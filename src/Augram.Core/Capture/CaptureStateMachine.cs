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

    private MouseButton _strokeButton;
    private MouseButton _owner;
    private bool _ownerIsStroke;
    private MouseButton _pressStrokeButton;
    private AnchorPlan _plan;
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
    }

    public IReadOnlyList<CaptureOutcome> Handle(CaptureEvent e) => e switch
    {
        CaptureEvent.ButtonDown down => OnButtonDown(down),
        CaptureEvent.ButtonUp up => OnButtonUp(up),
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

        if (State is CaptureState.Held or CaptureState.Drawing && _plan.Claims(_owner, _ownerIsStroke, down.Button))
        {
            // An After button: it joins the press, its click never reaches the app (A19: its up will be consumed too).
            _owed |= flag;
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
        CaptureOutcome? release = prior == CaptureState.HandedBack ? new CaptureOutcome.ReleaseHandedBack(_owner, down.X, down.Y) : null;
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
            CaptureState.HandedBack => [release!, CaptureOutcome.Suppress.Instance],
            _ => SuppressOnly,
        };
    }

    private IReadOnlyList<CaptureOutcome> OnButtonUp(CaptureEvent.ButtonUp up)
    {
        var flag = up.Button.Flag();
        _down &= ~flag;
        var owed = (_owed & flag) != 0;
        _owed &= ~flag;
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
            default:
                return SuppressOnly;
        }
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
