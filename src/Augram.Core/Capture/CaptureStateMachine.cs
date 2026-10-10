using Augram.Core.Abstractions;

namespace Augram.Core.Capture;

/// <summary>
/// The suppress-then-replay loop as a pure object (ADR-0002 §3, handoff §5): feed it
/// <see cref="CaptureEvent"/>s, it returns <see cref="CaptureOutcome"/>s. No hook, no timer, no
/// thread, no clock: time arrives on the events, and the Engine calls <see cref="Handle"/> from
/// exactly one thread (the engine worker). Each call is O(1) apart from appending one point.
/// <para>A press is owned by its <em>anchor</em>: the stroke button, or a button the press's <see cref="AnchorPlan"/> holds
/// back (a command holds it without the stroke button, "Right + wheel up"; learnings 0003 §4). The full contract, event by
/// event, is the table in <c>Capture/README.md</c>; the short version:</para>
/// <list type="bullet">
/// <item>An anchor down while Idle is suppressed unless capture is not allowed or the ignore key is held; Before keys and
/// buttons are what was held then. Another button down while Held or Drawing is suppressed and joins the press (After) when
/// the plan claims it for this anchor, else it passes and cancels a stroke press (A12) or hands a held-back anchor back.</item>
/// <item>Release, Held: nothing held → ReplayClick; stroke button with something held → ClickTrigger (resolved, relayed when
/// nothing fires); another anchor → ReplayClick with the After keys, or nothing when an After button took part.</item>
/// <item>A stroke button past the start distance draws; any other anchor is handed back (HandBack) and its release is
/// consumed and re-injected (ReleaseHandedBack), as it is after the hold-still time or another button.</item>
/// <item>Wheel while Held or Drawing: WheelFiring, the sets freeze; each tick is a WheelTrigger (marked AfterDrawing when the
/// stroke had started).</item>
/// <item>Every button's up is suppressed exactly when its down was (A19), whatever happened in between.</item>
/// <item>Another program's release of a button (plan 0005 decision 10): it is no longer down; a press it owns ends quietly,
/// with nothing replayed or fired, and its real release stays owed.</item>
/// </list>
/// <para>Changing <see cref="StrokeButton"/> mid-capture cancels a stroke press but keeps consuming the old button until
/// its release; <see cref="Reset"/> is the hard reset for when the OS state is unknown anyway (hook reinstalled).</para>
/// </summary>
public sealed partial class CaptureStateMachine
{
    private const int InitialPointCapacity = 256;

    private static readonly CaptureOutcome[] None = [];
    private static readonly CaptureOutcome[] SuppressOnly = [CaptureOutcome.Suppress.Instance];
    private static readonly CaptureOutcome[] PassThroughOnly = [CaptureOutcome.PassThrough.Instance];
    private static readonly CaptureOutcome[] AbandonDrawing = [CaptureOutcome.EndStroke.Instance, CaptureOutcome.Suppress.Instance];
    private static readonly CaptureOutcome[] CancelHeld = [new CaptureOutcome.Cancelled(CancelReason.HoldStill)];
    private static readonly CaptureOutcome[] CancelDrawing = [CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.HoldStill)];
    private static readonly CaptureOutcome[] OtherButtonCancelHeld = [CaptureOutcome.PassThrough.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)];
    private static readonly CaptureOutcome[] OtherButtonCancelDrawing = [CaptureOutcome.PassThrough.Instance, CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)];
    private static readonly CaptureOutcome[] EndedElsewhere = [new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)];
    private static readonly CaptureOutcome[] EndedElsewhereDrawing = [CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)];

    private MouseButton _strokeButton;
    private MouseButton _owner;
    private bool _ownerIsStroke;
    private MouseButton _pressStrokeButton;
    private AnchorPlan _plan;
    private AnchorDragDistances _drags;
    private HeldButtons _down;
    private HeldButtons _owed;
    private HeldButtons _before;
    private HeldButtons _after;
    private KeyModifiers _beforeKeys;
    private KeyModifiers _afterKeys;
    private CapturePoint _start;
    private CapturePoint _lastRecorded;
    private (int X, int Y) _pointer;
    private bool _wheelAfterDrawing;
    private long _deadlineMs;
    private List<CapturePoint> _points = [];

    // The button whose button trigger fired and whose chord is not over yet (plan 0005): its end releases what it holds.
    private MouseButton? _firing;

    public CaptureStateMachine(MouseButton strokeButton, CaptureThresholds? thresholds = null)
    {
        _strokeButton = strokeButton;
        _owner = strokeButton;
        Thresholds = thresholds ?? CaptureThresholds.Default;
    }

    public CaptureState State { get; private set; }

    /// <summary>Takes effect on the next event; safe to swap at any time (live-save, A3).</summary>
    public CaptureThresholds Thresholds { get; set; }

    /// <summary>
    /// The button that starts a capture. Setting it while a stroke press is held cancels it (state Cancelled) but keeps
    /// owning the old button until its release is consumed; a press owned by another anchor goes on. The caller ends any
    /// trail it is showing; this setter cannot return outcomes.
    /// </summary>
    public MouseButton StrokeButton
    {
        get => _strokeButton;
        set
        {
            _strokeButton = value;
            if (State != CaptureState.Idle && _ownerIsStroke && value != _owner)
            {
                State = CaptureState.Cancelled;
            }
        }
    }

    /// <summary>The button that owns the press in progress, or the stroke button while Idle.</summary>
    public MouseButton ActiveButton => State == CaptureState.Idle ? _strokeButton : _owner;

    /// <summary>The handed-back anchor whose injected down still waits for its release, or null: a hard reset must release it.</summary>
    public MouseButton? HandedBackButton => State == CaptureState.HandedBack ? _owner : null;

    /// <summary>True while a button other than the active one is physically down, as far as this machine has seen.</summary>
    public bool IsOtherButtonDown(MouseButton button) => button != ActiveButton && _down.Has(button);

    /// <summary>Buttons whose down was suppressed and whose up is still owed (A19), the owner included.</summary>
    public HeldButtons OwedButtons => _owed;

    /// <summary>Hard reset to Idle, forgetting every consumed down. Only for when the hook itself was reset; the caller releases <see cref="HandedBackButton"/> first.</summary>
    public void Reset()
    {
        State = CaptureState.Idle;
        _owner = _strokeButton;
        _ownerIsStroke = false;
        _down = HeldButtons.None;
        _owed = HeldButtons.None;
        _points = [];
        _firing = null;
    }

    public IReadOnlyList<CaptureOutcome> Handle(CaptureEvent e) => e switch
    {
        CaptureEvent.ButtonDown down => OnButtonDown(down),
        CaptureEvent.ButtonUp up => OnButtonUp(up),
        CaptureEvent.ButtonReleasedElsewhere released => OnReleasedElsewhere(released),
        CaptureEvent.Move move => OnMove(move),
        CaptureEvent.Wheel wheel => OnWheel(wheel),
        CaptureEvent.Tick tick => OnTick(tick),
        CaptureEvent.Key key => OnKey(key),
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, "Unknown capture event."),
    };

    private static long Squared(int value) => (long)value * value;

    private IReadOnlyList<CaptureOutcome> OnButtonDown(CaptureEvent.ButtonDown down)
    {
        var flag = down.Button.Flag();
        _down |= flag;
        if (State == CaptureState.Idle || down.Button == _owner)
        {
            return OnAnchorDown(down, flag);
        }

        if (State == CaptureState.ButtonFiring)
        {
            return OnButtonWhileFiring(down, flag);
        }

        if (State is CaptureState.Held or CaptureState.Drawing && _plan.Claims(_owner, _ownerIsStroke, down.Button))
        {
            _owed |= flag;
            if (State == CaptureState.Held && !_ownerIsStroke && _plan.Fires(_owner, down.Button))
            {
                // A button trigger (plan 0005): it fires at this press, and the press is frozen until the anchor's release.
                State = CaptureState.ButtonFiring;
                return Fire(down.Button, null);
            }

            // An After button: it joins the press, its click never reaches the app (A19: its up will be consumed too).
            _after |= flag;
            Push(down.TimestampMs);
            return SuppressOnly;
        }

        _owed &= ~flag;
        return State switch
        {
            CaptureState.Held when !_ownerIsStroke => HandBackNow(down.X, down.Y, CaptureOutcome.PassThrough.Instance),
            CaptureState.Drawing => Cancel(OtherButtonCancelDrawing),
            CaptureState.Held or CaptureState.WheelFiring => Cancel(OtherButtonCancelHeld),
            _ => PassThroughOnly,
        };
    }

    /// <summary>A press with no press in progress, or the owner pressed again (its release was missed: hook reinstall, sleep).</summary>
    private IReadOnlyList<CaptureOutcome> OnAnchorDown(CaptureEvent.ButtonDown down, HeldButtons flag)
    {
        var prior = State;
        var isAnchor = down.Button == _strokeButton || down.Plan.IsAnchor(down.Button);
        // The owner again while its press was handed back or firing: what the missed release would have ended ends first.
        CaptureOutcome? release = prior == CaptureState.HandedBack ? new CaptureOutcome.ReleaseHandedBack(_owner, down.X, down.Y) : Ended();
        if (!isAnchor || !down.CaptureAllowed || down.IgnoreKeyHeld)
        {
            _owed &= ~flag;
            ToIdle();
            return release is null ? PassThroughOnly : [release, CaptureOutcome.PassThrough.Instance];
        }

        _owner = down.Button;
        _ownerIsStroke = down.Button == _strokeButton;
        _pressStrokeButton = _strokeButton;
        _plan = down.Plan;
        _drags = down.Drags;
        _owed |= flag;
        _before = _down & ~flag;
        _beforeKeys = down.Modifiers & PressHold.TrackedKeys;
        _after = HeldButtons.None;
        _afterKeys = KeyModifiers.None;
        _start = new CapturePoint(down.X, down.Y, down.TimestampMs);
        _lastRecorded = _start;
        _pointer = (down.X, down.Y);
        _points = new List<CapturePoint>(InitialPointCapacity) { _start };
        _deadlineMs = down.TimestampMs + Thresholds.CancelDelayMs;
        State = CaptureState.Held;
        return prior switch
        {
            CaptureState.Drawing => AbandonDrawing,
            _ when release is not null => [release, CaptureOutcome.Suppress.Instance],
            _ => SuppressOnly,
        };
    }

    /// <summary>
    /// Another button while a button trigger holds the press (plan 0005): one the plan fires for this anchor fires again (the
    /// chord pressed anew; another button trigger of the anchor ends the held one first); any other passes and cancels, ending
    /// what is held, as another button cancels a wheel-fired press.
    /// </summary>
    private IReadOnlyList<CaptureOutcome> OnButtonWhileFiring(CaptureEvent.ButtonDown down, HeldButtons flag)
    {
        if (_plan.Fires(_owner, down.Button))
        {
            _owed |= flag;
            return Fire(down.Button, Ended());
        }

        _owed &= ~flag;
        State = CaptureState.Cancelled;
        return Ended() is { } ended
            ? [CaptureOutcome.PassThrough.Instance, ended, new CaptureOutcome.Cancelled(CancelReason.OtherButton)]
            : OtherButtonCancelHeld;
    }

    /// <summary>A button trigger fires for <paramref name="button"/>, after <paramref name="ended"/> (a chord it replaces); what the press held, without the fired button, goes with it.</summary>
    private CaptureOutcome[] Fire(MouseButton button, CaptureOutcome? ended)
    {
        _firing = button;
        var fire = new CaptureOutcome.ButtonTrigger(button, _start) { Hold = Hold() };
        return ended is null ? [CaptureOutcome.Suppress.Instance, fire] : [CaptureOutcome.Suppress.Instance, ended, fire];
    }

    /// <summary>The chord in progress ends (its output is released), or null when none is.</summary>
    private CaptureOutcome.ButtonTriggerEnded? Ended()
    {
        if (_firing is not { } firing)
        {
            return null;
        }

        _firing = null;
        return new CaptureOutcome.ButtonTriggerEnded(firing);
    }

    private IReadOnlyList<CaptureOutcome> OnButtonUp(CaptureEvent.ButtonUp up)
    {
        var flag = up.Button.Flag();
        _down &= ~flag;
        var owed = (_owed & flag) != 0;
        _owed &= ~flag;
        if (State == CaptureState.ButtonFiring && up.Button == _firing)
        {
            // The fired button is up, the anchor still held: the chord is over until it is pressed again.
            return [CaptureOutcome.Suppress.Instance, Ended()!];
        }

        if (State == CaptureState.Idle || up.Button != _owner)
        {
            return owed ? SuppressOnly : PassThroughOnly;
        }

        var state = State;
        var hold = Hold();
        var ownerIsStroke = _ownerIsStroke;
        ToIdle();
        switch (state)
        {
            case CaptureState.Held:
                return Released(up, hold, ownerIsStroke);
            case CaptureState.Drawing:
                var release = new CapturePoint(up.X, up.Y, up.TimestampMs);
                if (!release.IsSamePositionAs(_lastRecorded))
                {
                    _points.Add(release);
                }

                return [CaptureOutcome.Suppress.Instance, CaptureOutcome.EndStroke.Instance, new CaptureOutcome.StrokeComplete(_points, _start, up.Button) { Hold = hold }];
            case CaptureState.HandedBack:
                return [CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReleaseHandedBack(up.Button, up.X, up.Y)];
            case CaptureState.ButtonFiring when Ended() is { } ended:
                // The anchor is up before the fired button: the chord is over; the fired button's release is still owed.
                return [CaptureOutcome.Suppress.Instance, ended];
            default:
                return SuppressOnly;
        }
    }

    /// <summary>
    /// Another program posted a release of this button (plan 0005 decision 10): the OS has it up, and its real release may never
    /// come. It is no longer down here, so no later press holds it. A press it owns ends quietly: no click, no trigger, no
    /// recognition, and after a hand-back no injected release (the OS already got one). Its release stays owed, so a real one that
    /// still comes is consumed (A19: the OS never gets two). No input decision: the event itself is never suppressed.
    /// </summary>
    private IReadOnlyList<CaptureOutcome> OnReleasedElsewhere(CaptureEvent.ButtonReleasedElsewhere released)
    {
        _down &= ~released.Button.Flag();
        if (State == CaptureState.ButtonFiring && released.Button == _firing)
        {
            return [Ended()!];
        }

        if (State == CaptureState.Idle || released.Button != _owner)
        {
            return None;
        }

        var state = State;
        var ended = Ended();
        ToIdle();
        return state == CaptureState.Drawing ? EndedElsewhereDrawing
            : ended is not null ? [ended, new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)]
            : EndedElsewhere;
    }

    /// <summary>The owner released inside the start distance with no tick: a click, a click trigger, or nothing (an After button took part).</summary>
    private CaptureOutcome[] Released(CaptureEvent.ButtonUp up, PressHold hold, bool ownerIsStroke)
    {
        if (hold.IsEmpty)
        {
            return [CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(up.Button, up.X, up.Y)];
        }

        if (ownerIsStroke)
        {
            return [CaptureOutcome.Suppress.Instance, new CaptureOutcome.ClickTrigger(up.Button, up.X, up.Y, _start, hold)];
        }

        // Another anchor serves wheel triggers only: its click goes to the app, with any After keys, unless a button joined it.
        return hold.After != HeldButtons.None
            ? SuppressOnly
            : [CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(up.Button, up.X, up.Y) { AfterKeys = hold.AfterKeys }];
    }

    private PressHold Hold() => new(
        _ownerIsStroke ? HeldButtons.Stroke : _owner.Flag(),
        _ownerIsStroke ? _owner : _pressStrokeButton,
        _before,
        _beforeKeys,
        _after,
        _afterKeys);

    private void ToIdle()
    {
        State = CaptureState.Idle;
        _owner = _strokeButton;
        _ownerIsStroke = false;
    }
}
