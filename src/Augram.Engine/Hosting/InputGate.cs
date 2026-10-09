using System.Diagnostics;
using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// Everything that runs on the hook thread, in one class so it can be audited against CLAUDE.md
/// invariant 1: <see cref="Handle"/> reads four volatiles (state, stroke button, enabled, the ignore
/// list's answer), asks the <see cref="SuppressionShadow"/>, allocates one <see cref="CaptureEvent"/>,
/// <c>TryWrite</c>s one message, measures itself, returns the decision. A key event reads one volatile
/// (the hotkey-capture flag), asks the <see cref="KeySuppressionShadow"/>, and posts one message only
/// while capturing. Moves are forwarded only while a press is owed or the machine is not Idle. A full
/// queue drops moves and ticks; a press that cannot be enqueued is passed through and the shadow
/// restored; a release is still consumed (A19) and the worker resets the machine when it sees the drop
/// count. The worker publishes state and the applied stroke button here; the App writes
/// <see cref="Enabled"/> and <see cref="IgnoreKey"/>; <see cref="KeyCaptureController"/> writes the
/// capture flag; the <see cref="IgnoreListWatch"/> publishes the ignore list's answer
/// (<see cref="PublishIgnore"/>) and, while it watches the pointer, is handed each move's position.
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
    private int _state;
    private int _strokeButton;
    private int _ignoreKey;
    private bool _enabled;
    private long _worstHandlerTicks;
    private long _droppedMoves;
    private long _droppedButtons;
    private int _observeNextPress;
    private int _captureKeys;
    private int _ignoreState;
    private IgnoreListWatch? _watch;

    public InputGate(ChannelWriter<WorkerMessage> writer, IEventLog log, MouseButton strokeButton, KeyModifiers ignoreKey, bool enabled)
    {
        _writer = writer;
        _log = log;
        _strokeButton = (int)strokeButton;
        _ignoreKey = (int)ignoreKey;
        _enabled = enabled;
    }

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
    public int IgnoreState => Volatile.Read(ref _ignoreState);

    /// <summary>The watch's answer, read at the next stroke-button press only: a press already consumed still gets its release consumed (A19).</summary>
    public void PublishIgnore(int state) => Volatile.Write(ref _ignoreState, state);

    /// <summary>Hands moves to the ignore list's watch from now on (while it asks for them). Called once, before the hook starts.</summary>
    public void Attach(IgnoreListWatch watch) => _watch = watch;

    public void PublishState(CaptureState state) => Volatile.Write(ref _state, (int)state);

    public void PublishStrokeButton(MouseButton button) => Volatile.Write(ref _strokeButton, (int)button);

    public void ResetShadow()
    {
        _shadow.Reset();
        _keys.Reset();
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
        switch (input.Kind)
        {
            case RawInputKind.Move:
                // The shadow knows about a consumed press before the worker has run the machine; forward from that moment.
                if (state != CaptureState.Idle || _shadow.Owed.HasValue)
                {
                    Post(WorkerMessage.Input(new CaptureEvent.Move(input.X, input.Y, input.TimestampMs), false), critical: false);
                }

                // One volatile write and, while watching, one wake per batch: the watch looks the window up on its own thread.
                _watch?.PointerAt(input.X, input.Y);
                break;
            case RawInputKind.ButtonDown:
                // Disabled, paused by a focused "disable while focused" app, or over an ignored app: the press passes through,
                // decided from the watch's last answer; nothing here looks a window up (invariant 1).
                var allowed = Enabled && IgnoreState == 0;
                var ignore = (input.Modifiers & IgnoreKey) != 0;
                var owedBefore = _shadow.Owed;
                suppress = _shadow.Decide(in input, state, StrokeButton, allowed, ignore);
                var down = new CaptureEvent.ButtonDown(input.Button, input.X, input.Y, input.TimestampMs, allowed, ignore);
                if (!Post(WorkerMessage.Input(down, suppress), critical: true))
                {
                    _shadow.Restore(owedBefore);
                    suppress = false;
                }

                if (Interlocked.Exchange(ref _observeNextPress, 0) != 0)
                {
                    Post(WorkerMessage.ButtonObserved(input.Button), critical: true);
                }

                // Too late for this press, which was decided above from the last answer; it sets up the next one.
                _watch?.PointerAt(input.X, input.Y);
                break;
            case RawInputKind.ButtonUp:
                suppress = _shadow.Decide(in input, state, StrokeButton, false, false);
                Post(WorkerMessage.Input(new CaptureEvent.ButtonUp(input.Button, input.X, input.Y, input.TimestampMs), suppress), critical: true);
                break;
            case RawInputKind.Wheel:
                suppress = _shadow.Decide(in input, state, StrokeButton, false, false);
                if (!Post(WorkerMessage.Input(new CaptureEvent.Wheel(input.Wheel, input.X, input.Y, input.TimestampMs), suppress), critical: true))
                {
                    suppress = false;
                }

                break;
            case RawInputKind.KeyDown:
            case RawInputKind.KeyUp:
                var capturing = Volatile.Read(ref _captureKeys) != 0;
                suppress = _keys.Decide(in input, capturing);
                if (capturing)
                {
                    Post(WorkerMessage.KeyCaptured(KeyCaptureEvent.From(in input)), critical: false);
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
}
