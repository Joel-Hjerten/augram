namespace Augram.Core.Capture;

/// <summary>
/// The suppress-then-replay loop as a pure object (ADR-0002 §3, handoff §5): feed it
/// <see cref="CaptureEvent"/>s, it returns <see cref="CaptureOutcome"/>s. No hook, no timer, no
/// thread, no clock: time arrives on the events, and the Engine calls <see cref="Handle"/> from
/// exactly one thread (the hook thread). Each call is O(1) apart from appending one point.
/// <para>Contract (F1 with Joel's decisions A12, A13, A19; classic-source reference §2):</para>
/// <list type="table">
/// <item><term>ButtonDown, stroke button, Idle</term><description>
/// IgnoreKeyHeld or !CaptureAllowed: PassThrough, stay Idle. Else Suppress, remember start, go Held, deadline = t + CancelDelayMs.</description></item>
/// <item><term>Move, Held</term><description>
/// Record if at least MinSegmentPx from the last recorded point (pushes the deadline when ResetCancelDelayOnMovement).
/// If now at least StartDistancePx from start: BeginStroke(start) + StrokeProgress for every recorded point after start, go Drawing. Else nothing.</description></item>
/// <item><term>Move, Drawing</term><description>Same decimation; a recorded point emits StrokeProgress. Nothing otherwise.</description></item>
/// <item><term>Tick, Held or Drawing</term><description>t at or past the deadline: (EndStroke if Drawing) + Cancelled(HoldStill), go Cancelled. Else nothing.</description></item>
/// <item><term>ButtonUp, stroke button, Held</term><description>Suppress + ReplayClick at the release position, go Idle. The replay is the clean down+up pair.</description></item>
/// <item><term>ButtonUp, stroke button, Drawing</term><description>Suppress + EndStroke + StrokeComplete(start .. release), go Idle. Recognition is the Engine's job.</description></item>
/// <item><term>ButtonUp, stroke button, WheelFiring or Cancelled</term><description>Suppress (the down was consumed, so the up must be: A19), go Idle.</description></item>
/// <item><term>ButtonUp, stroke button, Idle</term><description>PassThrough (its down was passed through or never seen).</description></item>
/// <item><term>Wheel, Held or Drawing</term><description>Suppress + (EndStroke if Drawing) + WheelTrigger, go WheelFiring. The cancel deadline is abandoned.</description></item>
/// <item><term>Wheel, WheelFiring</term><description>Suppress + WheelTrigger again (every tick fires). Moves and Ticks are ignored in this state.</description></item>
/// <item><term>ButtonDown, other button, Held/Drawing/WheelFiring</term><description>PassThrough (the user's click must work) + (EndStroke if Drawing) + Cancelled(OtherButton), go Cancelled.</description></item>
/// <item><term>Other button Up; any other-button event while Idle or Cancelled</term><description>PassThrough. Other buttons are never consumed.</description></item>
/// <item><term>Wheel, Idle or Cancelled</term><description>PassThrough.</description></item>
/// <item><term>Move, Idle</term><description>PassThrough; the Engine need not forward these. Move while WheelFiring or Cancelled: nothing.</description></item>
/// <item><term>ButtonDown, stroke button, not Idle</term><description>The release was missed (hook reinstall, sleep). Abandon: (EndStroke if Drawing), then as from Idle.</description></item>
/// </list>
/// <para>Invariant (A19): every stroke-button down that returned Suppress is followed by a stroke-button up that
/// returns Suppress, never PassThrough. Changing <see cref="StrokeButton"/> mid-capture therefore cancels the
/// capture but keeps consuming the old button until its release; <see cref="Reset"/> is the hard reset for when
/// the OS state is unknown anyway (hook reinstalled).</para>
/// </summary>
public sealed class CaptureStateMachine
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

    private MouseButton _strokeButton;
    private MouseButton _activeButton;
    private CapturePoint _start;
    private CapturePoint _lastRecorded;
    private long _deadlineMs;
    private List<CapturePoint> _points = [];
    private int _otherButtonsDown;

    public CaptureStateMachine(MouseButton strokeButton, CaptureThresholds? thresholds = null)
    {
        _strokeButton = strokeButton;
        _activeButton = strokeButton;
        Thresholds = thresholds ?? CaptureThresholds.Default;
    }

    public CaptureState State { get; private set; }

    /// <summary>Takes effect on the next event; safe to swap at any time (live-save, A3).</summary>
    public CaptureThresholds Thresholds { get; set; }

    /// <summary>
    /// The button that starts a capture. Setting it while not Idle cancels the capture in progress
    /// (state Cancelled) but keeps owning the old button until its release is consumed.
    /// The caller ends any trail it is showing; this setter cannot return outcomes.
    /// </summary>
    public MouseButton StrokeButton
    {
        get => _strokeButton;
        set
        {
            _strokeButton = value;
            if (State == CaptureState.Idle)
            {
                _activeButton = value;
            }
            else if (value != _activeButton)
            {
                State = CaptureState.Cancelled;
            }
        }
    }

    /// <summary>True while a button other than the stroke button is physically down, as far as this machine has seen.</summary>
    public bool IsOtherButtonDown(MouseButton button) => (_otherButtonsDown & Bit(button)) != 0;

    /// <summary>Hard reset to Idle, forgetting any consumed down. Only for when the hook itself was reset.</summary>
    public void Reset()
    {
        State = CaptureState.Idle;
        _activeButton = _strokeButton;
        _otherButtonsDown = 0;
        _points = [];
    }

    public IReadOnlyList<CaptureOutcome> Handle(CaptureEvent e) => e switch
    {
        CaptureEvent.ButtonDown down => OnButtonDown(down),
        CaptureEvent.ButtonUp up => OnButtonUp(up),
        CaptureEvent.Move move => OnMove(move),
        CaptureEvent.Wheel wheel => OnWheel(wheel),
        CaptureEvent.Tick tick => OnTick(tick),
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, "Unknown capture event."),
    };

    private static int Bit(MouseButton button) => 1 << (int)button;

    private static long Squared(int value) => (long)value * value;

    private IReadOnlyList<CaptureOutcome> OnButtonDown(CaptureEvent.ButtonDown down)
    {
        if (down.Button != _activeButton)
        {
            _otherButtonsDown |= Bit(down.Button);
            return State switch
            {
                CaptureState.Idle or CaptureState.Cancelled => PassThroughOnly,
                CaptureState.Drawing => Cancel(OtherButtonCancelDrawing),
                _ => Cancel(OtherButtonCancelHeld),
            };
        }

        var abandonedDrawing = State == CaptureState.Drawing;
        if (!down.CaptureAllowed || down.IgnoreKeyHeld)
        {
            ToIdle();
            return PassThroughOnly;
        }

        _start = new CapturePoint(down.X, down.Y, down.TimestampMs);
        _lastRecorded = _start;
        _points = new List<CapturePoint>(InitialPointCapacity) { _start };
        _deadlineMs = down.TimestampMs + Thresholds.CancelDelayMs;
        State = CaptureState.Held;
        return abandonedDrawing ? AbandonDrawing : SuppressOnly;
    }

    private IReadOnlyList<CaptureOutcome> OnButtonUp(CaptureEvent.ButtonUp up)
    {
        if (up.Button != _activeButton)
        {
            _otherButtonsDown &= ~Bit(up.Button);
            return PassThroughOnly;
        }

        var state = State;
        ToIdle();
        switch (state)
        {
            case CaptureState.Idle:
                return PassThroughOnly;
            case CaptureState.Held:
                return [CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(up.Button, up.X, up.Y)];
            case CaptureState.Drawing:
                var release = new CapturePoint(up.X, up.Y, up.TimestampMs);
                if (!release.IsSamePositionAs(_lastRecorded))
                {
                    _points.Add(release);
                }

                return [CaptureOutcome.Suppress.Instance, CaptureOutcome.EndStroke.Instance, new CaptureOutcome.StrokeComplete(_points, _start, up.Button)];
            default:
                return SuppressOnly;
        }
    }

    private IReadOnlyList<CaptureOutcome> OnMove(CaptureEvent.Move move)
    {
        switch (State)
        {
            case CaptureState.Idle:
                return PassThroughOnly;
            case CaptureState.Held:
                var point = new CapturePoint(move.X, move.Y, move.TimestampMs);
                TryRecord(point);
                if (point.DistanceSquaredTo(_start) < Squared(Thresholds.StartDistancePx))
                {
                    return None;
                }

                State = CaptureState.Drawing;
                var outcomes = new CaptureOutcome[_points.Count];
                outcomes[0] = new CaptureOutcome.BeginStroke(_start);
                for (var i = 1; i < _points.Count; i++)
                {
                    outcomes[i] = new CaptureOutcome.StrokeProgress(_points[i]);
                }

                return outcomes;
            case CaptureState.Drawing:
                var next = new CapturePoint(move.X, move.Y, move.TimestampMs);
                return TryRecord(next) ? [new CaptureOutcome.StrokeProgress(next)] : None;
            default:
                return None;
        }
    }

    private IReadOnlyList<CaptureOutcome> OnWheel(CaptureEvent.Wheel wheel)
    {
        var state = State;
        if (state is CaptureState.Idle or CaptureState.Cancelled)
        {
            return PassThroughOnly;
        }

        State = CaptureState.WheelFiring;
        var trigger = new CaptureOutcome.WheelTrigger(wheel.Direction, _start);
        return state == CaptureState.Drawing
            ? [CaptureOutcome.Suppress.Instance, CaptureOutcome.EndStroke.Instance, trigger]
            : [CaptureOutcome.Suppress.Instance, trigger];
    }

    private IReadOnlyList<CaptureOutcome> OnTick(CaptureEvent.Tick tick)
    {
        if (State is not (CaptureState.Held or CaptureState.Drawing) || tick.TimestampMs < _deadlineMs)
        {
            return None;
        }

        return Cancel(State == CaptureState.Drawing ? CancelDrawing : CancelHeld);
    }

    /// <summary>Decimation: keep the point only if it is at least MinSegmentPx from the last kept one.</summary>
    private bool TryRecord(CapturePoint point)
    {
        if (point.DistanceSquaredTo(_lastRecorded) < Squared(Thresholds.MinSegmentPx))
        {
            return false;
        }

        _points.Add(point);
        _lastRecorded = point;
        if (Thresholds.ResetCancelDelayOnMovement)
        {
            _deadlineMs = point.TimestampMs + Thresholds.CancelDelayMs;
        }

        return true;
    }

    private IReadOnlyList<CaptureOutcome> Cancel(CaptureOutcome[] outcomes)
    {
        State = CaptureState.Cancelled;
        return outcomes;
    }

    private void ToIdle()
    {
        State = CaptureState.Idle;
        _activeButton = _strokeButton;
    }
}
