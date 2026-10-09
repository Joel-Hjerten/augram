using Augram.Core.Capture;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// Readable event sequences for state-machine tests. Keeps a clock and a pointer position so a
/// script reads like the physical gesture: <c>Down(Right, 100, 100).Move(140, 100).After(50).Up(Right)</c>.
/// Time only advances through <see cref="After"/>.
/// </summary>
internal sealed class EventScript
{
    private readonly List<CaptureEvent> _events = [];
    private long _now;
    private int _x;
    private int _y;

    public IReadOnlyList<CaptureEvent> Events => _events;

    public long Now => _now;

    public EventScript After(long ms)
    {
        _now += ms;
        return this;
    }

    public EventScript Down(MouseButton button, int x, int y, bool captureAllowed = true, bool ignoreKeyHeld = false)
    {
        MoveTo(x, y);
        return Add(new CaptureEvent.ButtonDown(button, x, y, _now, captureAllowed, ignoreKeyHeld, Modifiers, Plan));
    }

    public EventScript Down(MouseButton button) => Down(button, _x, _y);

    /// <summary>The anchor plan every later press carries (the window's, as the hook read it).</summary>
    public AnchorPlan Plan { get; set; }

    /// <summary>The keys every later press reports held.</summary>
    public Abstractions.KeyModifiers Modifiers { get; set; }

    public EventScript WithPlan(AnchorPlan plan)
    {
        Plan = plan;
        return this;
    }

    public EventScript Holding(Abstractions.KeyModifiers modifiers)
    {
        Modifiers = modifiers;
        return this;
    }

    /// <summary>A Ctrl/Alt/Shift/Win press during a press, with the hook's decision.</summary>
    public EventScript Key(Abstractions.KeyModifiers modifier, bool consumed = true) => Add(new CaptureEvent.Key(modifier, consumed, _now));

    public EventScript Up(MouseButton button, int x, int y)
    {
        MoveTo(x, y);
        return Add(new CaptureEvent.ButtonUp(button, x, y, _now));
    }

    public EventScript Up(MouseButton button) => Up(button, _x, _y);

    public EventScript Move(int x, int y)
    {
        MoveTo(x, y);
        return Add(new CaptureEvent.Move(x, y, _now));
    }

    public EventScript Wheel(WheelDirection direction) => Add(new CaptureEvent.Wheel(direction, _x, _y, _now));

    public EventScript Tick() => Add(new CaptureEvent.Tick(_now));

    /// <summary>Feeds every event in order and returns the outcomes per event, same index as <see cref="Events"/>.</summary>
    public IReadOnlyList<IReadOnlyList<CaptureOutcome>> RunOn(CaptureStateMachine machine)
    {
        var results = new List<IReadOnlyList<CaptureOutcome>>(_events.Count);
        foreach (var e in _events)
        {
            results.Add(machine.Handle(e));
        }

        return results;
    }

    /// <summary>Feeds every event and returns only the outcomes of the last one.</summary>
    public IReadOnlyList<CaptureOutcome> RunOnForLast(CaptureStateMachine machine) => RunOn(machine)[^1];

    private EventScript Add(CaptureEvent e)
    {
        _events.Add(e);
        return this;
    }

    private void MoveTo(int x, int y)
    {
        _x = x;
        _y = y;
    }
}
