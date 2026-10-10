using System.Diagnostics;
using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.HoldRemaps;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// Everything that runs on the hook thread, in one class so it can be audited against CLAUDE.md
/// invariant 1: <see cref="Handle"/> reads four volatiles (state, stroke button, enabled, the pointer's
/// answer: the ignore list's bits and the window's <see cref="AnchorPlan"/>), asks the <see cref="SuppressionShadow"/>,
/// allocates one <see cref="CaptureEvent"/>, <c>TryWrite</c>s one message, measures itself, returns the decision. A key
/// event reads one volatile (the hotkey-capture flag), asks the <see cref="KeySuppressionShadow"/> (which a held press may
/// claim a Ctrl/Alt/Shift/Win press for), and posts one message while capturing, and one for a modifier press while a
/// press is held. Hold remaps (F9) are asked first: the <see cref="HoldRemapShadow"/> decides from the foreground's
/// <see cref="HoldRemapPlan"/> (one more volatile, <see cref="PublishForeground"/>) and posts one <c>Hold</c> message per
/// decision while a hold is engaged; what it takes never reaches gesture capture. Moves are forwarded only while a press
/// is owed or the machine is not Idle; where the simulator re-posts drags (macOS) a move is also swallowed and posted as a
/// hold <c>Move</c> while the hold remap holds a button output (one field read, one more message). A full queue drops moves,
/// ticks and key notes (a move it could not post for a drag passes); a press that cannot be enqueued is passed through and
/// the shadow restored; a release is still consumed (A19) and the worker resets the machines when it sees the drop count.
/// The worker publishes state and the applied stroke button here; the App writes <see cref="Enabled"/> and
/// <see cref="IgnoreKey"/>; <see cref="KeyCaptureController"/> writes the capture flag; the <see cref="IgnoreListWatch"/>
/// publishes the pointer's answer (<see cref="PublishPointer"/>) and the foreground's hold remaps and, while it watches the
/// pointer, is handed each move's position.
/// </summary>
internal sealed class InputGate
{
    /// <summary><see cref="IgnoreState"/> bit: the pointer is over a window of an active ignored app; its stroke button passes through.</summary>
    public const int OverIgnoredApp = 1;

    /// <summary><see cref="IgnoreState"/> bit: a "disable while focused" app has focus; everything passes through, as if disabled.</summary>
    public const int PausedByFocus = 2;

    private readonly ChannelWriter<WorkerMessage> _writer;
    private readonly IEventLog _log;
    private readonly SuppressionShadow _shadow = new();
    private readonly KeySuppressionShadow _keys = new();
    private readonly HoldRemapShadow _hold = new();
    private readonly bool _repostsRemapDrags;
    private int _state;
    private int _strokeButton;
    private int _ignoreKey;
    private bool _enabled;
    private long _worstHandlerTicks;
    private long _droppedMoves;
    private long _droppedButtons;
    private int _observeNextPress;
    private int _captureKeys;
    private const int IgnoreBits = 2;
    private long _pointerAnswer;
    private long _pointerDrags;
    private HoldRemapPlan _foreground = HoldRemapPlan.Empty;
    private int _pointerX;
    private int _pointerY;
    private IgnoreListWatch? _watch;

    /// <param name="writer">The hook-to-worker channel.</param>
    /// <param name="log">Trace only, from the hook thread.</param>
    /// <param name="strokeButton">The stroke button until the worker publishes another.</param>
    /// <param name="ignoreKey">The ignore key.</param>
    /// <param name="enabled">The tray toggle.</param>
    /// <param name="repostsRemapDrags">The simulator's <see cref="IInputSimulator.RepostsRemapDrags"/>, read once: while a hold remap holds a button output, moves are swallowed and posted for the worker to re-post as drags (macOS).</param>
    public InputGate(ChannelWriter<WorkerMessage> writer, IEventLog log, MouseButton strokeButton, KeyModifiers ignoreKey, bool enabled, bool repostsRemapDrags = false)
    {
        _writer = writer;
        _log = log;
        _strokeButton = (int)strokeButton;
        _ignoreKey = (int)ignoreKey;
        _enabled = enabled;
        _repostsRemapDrags = repostsRemapDrags;
    }

    /// <summary>Moves are swallowed and re-posted as drags while a hold remap holds a button output (the simulator asked for it, macOS).</summary>
    public bool RepostsRemapDrags => _repostsRemapDrags;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled);
        set => Volatile.Write(ref _enabled, value);
    }

    public KeyModifiers IgnoreKey
    {
        get => (KeyModifiers)Volatile.Read(ref _ignoreKey);
        set => Volatile.Write(ref _ignoreKey, (int)value);
    }

    public MouseButton StrokeButton => (MouseButton)Volatile.Read(ref _strokeButton);

    public CaptureState State => (CaptureState)Volatile.Read(ref _state);

    public long DroppedMoveCount => Volatile.Read(ref _droppedMoves);

    /// <summary>True while a hotkey capture is armed: key presses are suppressed system-wide and reported to the worker.</summary>
    public bool KeysCaptured => Volatile.Read(ref _captureKeys) != 0;

    /// <summary>The ignore list's answer the next press reads: <see cref="OverIgnoredApp"/> and <see cref="PausedByFocus"/> bits, 0 for neither.</summary>
    public int IgnoreState => (int)(Volatile.Read(ref _pointerAnswer) & ((1 << IgnoreBits) - 1));

    /// <summary>The anchor plan for the window under the pointer as of the watch's last pass (per app, Joel 2026-10-09); <see cref="AnchorPlan.None"/> while nothing holds a button besides the stroke button.</summary>
    public AnchorPlan Plan => new(Volatile.Read(ref _pointerAnswer) >> IgnoreBits);

    /// <summary>The drag distance per anchor over the same window (plan 0004), handed to the machine with each press.</summary>
    public AnchorDragDistances Drags => new(Volatile.Read(ref _pointerDrags));

    /// <summary>The hold remaps of the app in front as of the watch's last pass (F9); <see cref="HoldRemapPlan.Empty"/> while none can match.</summary>
    public HoldRemapPlan ForegroundPlan => Volatile.Read(ref _foreground);

    /// <summary>For tests and the worker: the hook's hold remap record.</summary>
    public HoldRemapShadow Hold => _hold;

    /// <summary>The watch's ignore answer, keeping the plan: read at the next press only, so a press already consumed still gets its release consumed (A19).</summary>
    public void PublishIgnore(int state) => PublishPointer(state, Plan, Drags);

    /// <summary>
    /// The watch's whole answer for the window under the pointer, as one volatile the hook reads at a press: the ignore bits
    /// and the anchor plan. Single writer (the watch's thread). The drag distances go in a second volatile, written first: a
    /// press that reads them from another pass than its plan only hands back at another distance, it decides nothing else.
    /// </summary>
    public void PublishPointer(int ignoreState, AnchorPlan plan, AnchorDragDistances drags = default)
    {
        Volatile.Write(ref _pointerDrags, drags.Bits);
        Volatile.Write(ref _pointerAnswer, (plan.Bits << IgnoreBits) | (uint)(ignoreState & ((1 << IgnoreBits) - 1)));
    }

    /// <summary>
    /// The watch's answer for the app in front: its hold remaps, read by the hook at a hold key's press only (plan 0002
    /// decision 8), so a hold in progress keeps the plan it started with. Single writer (the watch's thread).
    /// </summary>
    public void PublishForeground(HoldRemapPlan plan) => Volatile.Write(ref _foreground, plan ?? HoldRemapPlan.Empty);

    /// <summary>Worker: the injections of one message the hook counted as a pending replay are made.</summary>
    public void HoldReplayDone() => _hold.ReplayDone();

    /// <summary>Hands moves to the ignore list's watch from now on (while it asks for them). Called once, before the hook starts.</summary>
    public void Attach(IgnoreListWatch watch) => _watch = watch;

    public void PublishState(CaptureState state) => Volatile.Write(ref _state, (int)state);

    public void PublishStrokeButton(MouseButton button) => Volatile.Write(ref _strokeButton, (int)button);

    public void ResetShadow()
    {
        _shadow.Reset();
        _keys.Reset();
        _hold.Reset();
    }

    /// <summary>The hotkey-capture flag (F5). Clearing it stops new presses being swallowed at once; owed releases still are.</summary>
    public void CaptureKeys(bool armed) => Volatile.Write(ref _captureKeys, armed ? 1 : 0);

    /// <summary>Arms (or disarms) a one-shot report of the next physical press, posted to the worker as <c>ButtonObserved</c>; the press itself is handled as usual.</summary>
    public void ObserveNextPress(bool armed) => Volatile.Write(ref _observeNextPress, armed ? 1 : 0);

    public double TakeWorstHandlerMicroseconds() => Interlocked.Exchange(ref _worstHandlerTicks, 0) * 1_000_000.0 / Stopwatch.Frequency;

    public (long Moves, long Buttons) TakeDropCounts() => (Interlocked.Exchange(ref _droppedMoves, 0), Interlocked.Exchange(ref _droppedButtons, 0));

    public bool Post(WorkerMessage message, bool critical)
    {
        if (_writer.TryWrite(message))
        {
            return true;
        }

        Interlocked.Increment(ref critical ? ref _droppedButtons : ref _droppedMoves);
        return false;
    }

    /// <summary>Hook thread.</summary>
    public bool Handle(in RawInput input)
    {
        var started = Stopwatch.GetTimestamp();
        var state = State;
        var suppress = false;
        if (input.Kind != RawInputKind.Move)
        {
            EndHoldIfFocusMoved(input.TimestampMs);
        }

        switch (input.Kind)
        {
            case RawInputKind.Move:
                _pointerX = input.X;
                _pointerY = input.Y;
                // macOS: a posted button moves only with drags posted for it (learnings 0005). While the hold remap holds a
                // button output the physical move is swallowed and the worker re-posts it, in order with the hold messages. A
                // move that cannot be enqueued is counted and passes (the next drag's delta still covers it).
                if (_repostsRemapDrags && _hold.HoldsButtonOutput)
                {
                    suppress = Post(WorkerMessage.Hold(new HoldRemapEvent.Move(input.X, input.Y, input.TimestampMs), hookSuppressed: true), critical: false);
                }

                // The shadow knows about a consumed press before the worker has run the machine; forward from that moment.
                if (state != CaptureState.Idle || _shadow.Owed.HasValue)
                {
                    Post(WorkerMessage.Input(new CaptureEvent.Move(input.X, input.Y, input.TimestampMs), false), critical: false);
                }

                // One volatile write and, while watching, one wake per batch: the watch looks the window up on its own thread.
                _watch?.PointerAt(input.X, input.Y);
                break;
            case RawInputKind.ButtonDown:
                _pointerX = input.X;
                _pointerY = input.Y;
                // An input of the held hold remap belongs to it, even the stroke button: gesture capture never sees it.
                if (DecideHoldButton(in input))
                {
                    suppress = true;
                }
                else
                {
                    // Disabled, paused by a focused "disable while focused" app, or over an ignored app: the press passes through,
                    // decided from the watch's last answer; the same answer says which buttons are anchors over this window. Nothing
                    // here looks a window up (invariant 1).
                    var answer = Volatile.Read(ref _pointerAnswer);
                    var allowed = Enabled && (answer & ((1 << IgnoreBits) - 1)) == 0;
                    var plan = new AnchorPlan(answer >> IgnoreBits);
                    var drags = new AnchorDragDistances(Volatile.Read(ref _pointerDrags));
                    var ignore = (input.Modifiers & IgnoreKey) != 0;
                    // The press's Before keys: the library's mask, limited to keys this hook saw go down (a stale mask holds no phantom key).
                    var press = input with { Modifiers = input.Modifiers & _keys.HeldModifiers() };
                    var before = _shadow.Save();
                    suppress = _shadow.Decide(in press, state, StrokeButton, allowed, ignore, plan);
                    var down = new CaptureEvent.ButtonDown(input.Button, input.X, input.Y, input.TimestampMs, allowed, ignore, press.Modifiers, plan, drags);
                    if (!Post(WorkerMessage.Input(down, suppress), critical: true))
                    {
                        _shadow.Restore(before);
                        suppress = false;
                    }
                }

                if (Interlocked.Exchange(ref _observeNextPress, 0) != 0)
                {
                    Post(WorkerMessage.ButtonObserved(input.Button), critical: true);
                }

                // Too late for this press, which was decided above from the last answer; it sets up the next one.
                _watch?.PointerAt(input.X, input.Y);
                break;
            case RawInputKind.ButtonUp:
                _pointerX = input.X;
                _pointerY = input.Y;
                if (DecideHoldButton(in input))
                {
                    suppress = true;
                    break;
                }

                suppress = _shadow.Decide(in input, state, StrokeButton, false, false);
                Post(WorkerMessage.Input(new CaptureEvent.ButtonUp(input.Button, input.X, input.Y, input.TimestampMs), suppress), critical: true);
                break;
            case RawInputKind.Wheel:
                if (DecideHoldWheel(in input))
                {
                    suppress = true;
                    break;
                }

                suppress = _shadow.Decide(in input, state, StrokeButton, false, false);
                if (!Post(WorkerMessage.Input(new CaptureEvent.Wheel(input.Wheel, input.X, input.Y, input.TimestampMs), suppress), critical: true))
                {
                    suppress = false;
                }

                break;
            case RawInputKind.KeyDown:
            case RawInputKind.KeyUp:
                var capturing = Volatile.Read(ref _captureKeys) != 0;
                var held = DecideHoldKey(in input, capturing);
                // The hold remap's decision wins over the record's age: a hold key held without repeats outlives LostReleaseAfterMs.
                suppress = _keys.Decide(in input, capturing, _shadow.KeyClaim(state), claim: held) || held;
                if (capturing)
                {
                    Post(WorkerMessage.KeyCaptured(KeyCaptureEvent.From(in input)), critical: false);
                }

                // While a press is held, Ctrl/Alt/Shift/Win presses go to the machine with the decision: a consumed one is an After key.
                if (!held && input.Kind == RawInputKind.KeyDown && _shadow.Owed.HasValue && KeySuppressionShadow.ModifierOf(input.Key) is var modifier and not KeyModifiers.None)
                {
                    Post(WorkerMessage.Input(new CaptureEvent.Key(modifier, suppress, input.TimestampMs), suppress), critical: false);
                }

                break;
        }

        if (input.Kind != RawInputKind.Move && _log.IsEnabled(EventLevel.Trace))
        {
            _log.Trace(LogSources.Hook, "Input", ("kind", input.Kind), ("button", input.Button), ("key", input.Key), ("x", input.X), ("y", input.Y), ("suppress", suppress), ("state", state));
        }

        var elapsed = Stopwatch.GetTimestamp() - started;
        if (elapsed > Volatile.Read(ref _worstHandlerTicks))
        {
            Volatile.Write(ref _worstHandlerTicks, elapsed);
        }

        return suppress;
    }

    /// <summary>
    /// A hold key held with nothing owed, and the published plan is now another app group's or none (Joel, 2026-10-10): the
    /// hold ends here, before this event is decided, and the machine is told in order (<see cref="HoldRemapEvent.FocusMoved"/>).
    /// Seen at the next button, wheel or key event, which is the first moment it matters. One volatile read while holding.
    /// </summary>
    private void EndHoldIfFocusMoved(long timestampMs)
    {
        if (!_hold.Holding || !_hold.FocusMovedFrom(Volatile.Read(ref _foreground).GroupId))
        {
            return;
        }

        var before = _hold.Save();
        _hold.EndByFocus();
        if (!Post(WorkerMessage.Hold(new HoldRemapEvent.FocusMoved(timestampMs), hookSuppressed: true), critical: true))
        {
            _hold.Restore(before);
        }
    }

    /// <summary>
    /// A button's down or up while a hold is engaged: the hold remap's decision, posted as one <c>Hold</c> message. A press
    /// that cannot be enqueued is undone (it goes on to gesture capture as if the hold had passed it); a release is decided
    /// all the same (A19), and the worker resets the machine when it sees the drop.
    /// </summary>
    private bool DecideHoldButton(in RawInput input)
    {
        if (!_hold.Engaged)
        {
            return false;
        }

        var isDown = input.Kind == RawInputKind.ButtonDown;
        var before = _hold.Save();
        var taken = _hold.DecideButton(in input);
        var e = new HoldRemapEvent.Button(input.Button, isDown, input.X, input.Y, input.TimestampMs);
        if (Post(WorkerMessage.Hold(e, taken), critical: true) || !isDown)
        {
            return taken;
        }

        _hold.Restore(before);
        return false;
    }

    /// <summary>A wheel notch while a hold key is held: the hold remap's decision (every notch counts as used), posted; undone when it cannot be enqueued.</summary>
    private bool DecideHoldWheel(in RawInput input)
    {
        if (!_hold.Holding)
        {
            return false;
        }

        var before = _hold.Save();
        var taken = _hold.DecideWheel(in input);
        if (Post(WorkerMessage.Hold(new HoldRemapEvent.Wheel(input.Wheel, input.X, input.Y, input.TimestampMs), taken), critical: true))
        {
            return taken;
        }

        _hold.Restore(before);
        return false;
    }

    /// <summary>
    /// A key event, hold remaps first: the hold key's press is offered when the foreground's plan has it and nothing else
    /// stands in the way (enabled, not paused by a focused app, the ignore key up, no hotkey capture, no gesture press owned, a
    /// fresh press; "over an ignored app" does not count: Blender is ignored that way and keeps its hold remap). One message
    /// per decision; a press that cannot be enqueued is undone, a release is still swallowed.
    /// </summary>
    private bool DecideHoldKey(in RawInput input, bool capturing)
    {
        var isUp = input.Kind == RawInputKind.KeyUp;
        var fresh = !isUp && _keys.IsFreshPress(in input);
        HoldRemapEntry? candidate = null;
        HoldRemapPlan? plan = null;
        if (fresh && !capturing && !_hold.Holding)
        {
            plan = Volatile.Read(ref _foreground);
            if (!plan.IsEmpty
                && Enabled
                && (IgnoreState & PausedByFocus) == 0
                && (input.Modifiers & IgnoreKey) == 0
                && !_shadow.Owed.HasValue)
            {
                candidate = plan.Find(input.Key);
            }
        }

        if (candidate is null && _hold.Idle && (!fresh || capturing || _hold.PendingReplays == 0))
        {
            return false;
        }

        var before = _hold.Save();
        var decision = _hold.DecideKey(input.Key, isUp, fresh, input.TimestampMs, candidate, capturing, plan?.GroupId);
        if (decision.Verdict == HoldKeyVerdict.None)
        {
            return false;
        }

        HoldRemapEvent e = decision.Verdict switch
        {
            HoldKeyVerdict.HoldDown => new HoldRemapEvent.HoldDown(candidate!, input.TimestampMs),
            HoldKeyVerdict.HoldUp => new HoldRemapEvent.HoldUp(input.Key, input.TimestampMs),
            _ => new HoldRemapEvent.Key(input.Key, decision.Phase, input.TimestampMs, _pointerX, _pointerY),
        };
        var message = decision.Verdict == HoldKeyVerdict.Ordered
            ? WorkerMessage.HoldReplay((HoldRemapEvent.Key)e)
            : WorkerMessage.Hold(e, decision.Suppress, decision.AwaitsInjection, decision.Verdict == HoldKeyVerdict.HoldDown ? plan?.GroupName : null);
        if (decision.AwaitsInjection)
        {
            _hold.ReplayPosted();
        }

        if (Post(message, critical: true))
        {
            return decision.Suppress;
        }

        if (decision.AwaitsInjection)
        {
            _hold.ReplayDone();
        }

        if (isUp)
        {
            return decision.Suppress;
        }

        _hold.Restore(before);
        return false;
    }
}
