using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>Everything the machine must leave alone: ignore key, disallowed windows, other buttons, idle input.</summary>
public sealed class PassThroughTests
{
    private const MouseButton Stroke = MouseButton.Right;

    [Fact]
    public void IgnoreKeyHeld_PassesThePressThrough_AndStaysIdle()
    {
        var machine = new CaptureStateMachine(Stroke);

        var results = new EventScript().Down(Stroke, 100, 100, ignoreKeyHeld: true).Move(200, 200).Up(Stroke).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[0]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[1]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[2]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void CaptureNotAllowed_PassesThePressThrough_AndStaysIdle()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100, captureAllowed: false).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void IgnoreKeyIsReadPerPress_NotRemembered()
    {
        var machine = new CaptureStateMachine(Stroke);

        var results = new EventScript()
            .Down(Stroke, 100, 100, ignoreKeyHeld: true).Up(Stroke)
            .Down(Stroke, 100, 100).Up(Stroke)
            .RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[2]);
        Assert.Contains(CaptureOutcome.Suppress.Instance, results[3]);
    }

    [Fact]
    public void OtherButtonsWhileIdle_PassThrough_BothWays()
    {
        var machine = new CaptureStateMachine(Stroke);

        var results = new EventScript().Down(MouseButton.Left, 1, 1).Up(MouseButton.Left).RunOn(machine);

        Assert.All(results, r => Assert.Equal([CaptureOutcome.PassThrough.Instance], r));
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void OtherButtonAlreadyDown_DoesNotStopACapture_AndItsReleasePassesThrough()
    {
        var machine = new CaptureStateMachine(Stroke);

        var results = new EventScript().Down(MouseButton.Left, 1, 1).Down(Stroke, 100, 100).Up(MouseButton.Left).Move(150, 100).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[1]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[2]);
        Assert.Equal(CaptureState.Drawing, machine.State);
    }

    [Fact]
    public void MoveWhileIdle_PassesThrough()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Move(5, 5).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], outcomes);
    }

    [Fact]
    public void TickWhileIdle_EmitsNothing()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().After(100_000).Tick().RunOnForLast(machine);

        Assert.Empty(outcomes);
    }

    [Fact]
    public void StrokeButtonUpWhileIdle_PassesThrough()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Up(Stroke, 1, 1).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void RepeatedStrokeButtonDown_AbandonsTheCurrentCapture_AndStartsOver()
    {
        var machine = new CaptureStateMachine(Stroke);

        var results = new EventScript().Down(Stroke, 100, 100).Move(150, 100).Down(Stroke, 300, 300).Up(Stroke).RunOn(machine);

        Assert.Equal([CaptureOutcome.EndStroke.Instance, CaptureOutcome.Suppress.Instance], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Stroke, 300, 300)], results[3]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }
}
