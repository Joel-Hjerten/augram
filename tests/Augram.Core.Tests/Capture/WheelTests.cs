using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>Wheel-while-holding, StrokesPlus semantics (F1, reference §2): fires any time, every tick, trail stops, release fires nothing.</summary>
public sealed class WheelTests
{
    private const MouseButton Stroke = MouseButton.Right;

    [Fact]
    public void WheelWhileHeld_SuppressesAndFires_AtTheStartPoint()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(110, 100).Wheel(WheelDirection.Up).RunOnForLast(machine);

        Assert.Equal(
            [CaptureOutcome.Suppress.Instance, new CaptureOutcome.WheelTrigger(WheelDirection.Up, new CapturePoint(100, 100, 0))],
            outcomes);
        Assert.Equal(CaptureState.WheelFiring, machine.State);
    }

    [Fact]
    public void WheelWhileDrawing_EndsTheTrail_ThenFires()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(150, 100).Wheel(WheelDirection.Down).RunOnForLast(machine);

        Assert.Equal(
            [
                CaptureOutcome.Suppress.Instance,
                CaptureOutcome.EndStroke.Instance,
                new CaptureOutcome.WheelTrigger(WheelDirection.Down, new CapturePoint(100, 100, 0)),
            ],
            outcomes);
    }

    [Fact]
    public void ThreeTicks_FireThreeTimes()
    {
        var machine = new CaptureStateMachine(Stroke);
        var script = new EventScript().Down(Stroke, 100, 100).Wheel(WheelDirection.Down).Wheel(WheelDirection.Down).Wheel(WheelDirection.Up);

        var results = script.RunOn(machine);

        var triggers = results.Skip(1).Select(r => Assert.IsType<CaptureOutcome.WheelTrigger>(r[^1]).Direction).ToList();
        Assert.Equal([WheelDirection.Down, WheelDirection.Down, WheelDirection.Up], triggers);
        Assert.All(results.Skip(1), r => Assert.Contains(CaptureOutcome.Suppress.Instance, r));
    }

    [Fact]
    public void MovesWhileWheelFiring_AreIgnored()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Wheel(WheelDirection.Up).Move(300, 300).RunOnForLast(machine);

        Assert.Empty(outcomes);
        Assert.Equal(CaptureState.WheelFiring, machine.State);
    }

    [Fact]
    public void CancelDeadline_IsAbandonedAfterAWheelTick()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 100));

        var outcomes = new EventScript().Down(Stroke, 100, 100).Wheel(WheelDirection.Up).After(5000).Tick().RunOnForLast(machine);

        Assert.Empty(outcomes);
        Assert.Equal(CaptureState.WheelFiring, machine.State);
    }

    [Fact]
    public void StrokeButtonUpWhileWheelFiring_IsSuppressed_WithoutStrokeComplete()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(150, 100).Wheel(WheelDirection.Up).Up(Stroke).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void OtherButtonWhileWheelFiring_Cancels()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Wheel(WheelDirection.Up).Down(MouseButton.Left).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)], outcomes);
        Assert.Equal(CaptureState.Cancelled, machine.State);
    }

    [Fact]
    public void WheelWhileIdle_PassesThrough()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Wheel(WheelDirection.Up).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }
}
