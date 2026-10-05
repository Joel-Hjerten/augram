using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>Hold-still and other-button cancels (A12, A13), and the consumed release that must follow (A19).</summary>
public sealed class CancelTests
{
    private const MouseButton Stroke = MouseButton.Right;

    [Fact]
    public void TickBeforeDeadline_EmitsNothing()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000));

        var outcomes = new EventScript().Down(Stroke, 100, 100).After(999).Tick().RunOnForLast(machine);

        Assert.Empty(outcomes);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void TickAtDeadlineWhileHeld_CancelsHoldStill()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000));

        var outcomes = new EventScript().Down(Stroke, 100, 100).After(1000).Tick().RunOnForLast(machine);

        Assert.Equal([new CaptureOutcome.Cancelled(CancelReason.HoldStill)], outcomes);
        Assert.Equal(CaptureState.Cancelled, machine.State);
    }

    [Fact]
    public void TickPastDeadlineWhileDrawing_EndsStrokeAndCancels()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000));

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(150, 100).After(1500).Tick().RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.HoldStill)], outcomes);
        Assert.Equal(CaptureState.Cancelled, machine.State);
    }

    [Fact]
    public void RecordedMovement_PushesTheDeadline()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000, MinSegmentPx: 6));
        var script = new EventScript().Down(Stroke, 100, 100)
            .After(800).Move(110, 100)   // recorded while Held: deadline becomes 1800
            .After(800).Move(150, 100)   // t=1600, recorded, Drawing: deadline becomes 2600
            .After(900).Tick()           // t=2500: still inside
            .After(100).Tick();          // t=2600: expired

        var results = script.RunOn(machine);

        Assert.Empty(results[3]);
        Assert.Equal([CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.HoldStill)], results[4]);
    }

    [Fact]
    public void UnrecordedJitter_DoesNotPushTheDeadline()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000, MinSegmentPx: 6));

        var outcomes = new EventScript().Down(Stroke, 100, 100).After(500).Move(102, 101).After(500).Tick().RunOnForLast(machine);

        Assert.Equal([new CaptureOutcome.Cancelled(CancelReason.HoldStill)], outcomes);
    }

    [Fact]
    public void WithResetOff_TheDeadlineIsFixedFromThePress()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 1000, ResetCancelDelayOnMovement: false));

        var outcomes = new EventScript().Down(Stroke, 100, 100).After(900).Move(150, 100).After(100).Tick().RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.HoldStill)], outcomes);
    }

    [Fact]
    public void OtherButtonDownWhileDrawing_CancelsAndPassesThatButtonThrough_BothWays()
    {
        var machine = new CaptureStateMachine(Stroke);
        var script = new EventScript().Down(Stroke, 100, 100).Move(150, 100).Down(MouseButton.Left).Up(MouseButton.Left);

        var results = script.RunOn(machine);

        Assert.Equal(
            [CaptureOutcome.PassThrough.Instance, CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)],
            results[2]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[3]);
        Assert.Equal(CaptureState.Cancelled, machine.State);
        Assert.False(machine.IsOtherButtonDown(MouseButton.Left));
    }

    [Fact]
    public void OtherButtonDownWhileHeld_CancelsWithoutEndStroke()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Down(MouseButton.Middle).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)], outcomes);
        Assert.True(machine.IsOtherButtonDown(MouseButton.Middle));
    }

    [Fact]
    public void StrokeButtonUpWhileCancelled_IsSuppressed_AndGoesIdle()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Down(MouseButton.Left).Up(Stroke).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void MovesAndTicksWhileCancelled_AreIgnored_AndWheelPassesThrough()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 100));
        var script = new EventScript().Down(Stroke, 100, 100).After(100).Tick().Move(200, 200).After(1000).Tick().Wheel(WheelDirection.Up);

        var results = script.RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Empty(results[3]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[4]);
        Assert.Equal(CaptureState.Cancelled, machine.State);
    }

    [Fact]
    public void ChangingStrokeButtonMidCapture_KeepsConsumingTheOldButtonUntilRelease()
    {
        var machine = new CaptureStateMachine(Stroke);
        new EventScript().Down(Stroke, 100, 100).Move(150, 100).RunOn(machine);

        machine.StrokeButton = MouseButton.Middle;
        var oldUp = machine.Handle(new CaptureEvent.ButtonUp(Stroke, 150, 100, 0));
        var newDown = machine.Handle(new CaptureEvent.ButtonDown(MouseButton.Middle, 0, 0, 0));

        Assert.Equal([CaptureOutcome.Suppress.Instance], oldUp);
        Assert.Equal([CaptureOutcome.Suppress.Instance], newDown);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void Reset_GoesIdle_AndForgetsEverything()
    {
        var machine = new CaptureStateMachine(Stroke);
        new EventScript().Down(Stroke, 100, 100).Move(150, 100).Down(MouseButton.Left).RunOn(machine);

        machine.Reset();

        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.False(machine.IsOtherButtonDown(MouseButton.Left));
        Assert.Equal([CaptureOutcome.PassThrough.Instance], machine.Handle(new CaptureEvent.ButtonUp(Stroke, 150, 100, 0)));
    }
}
