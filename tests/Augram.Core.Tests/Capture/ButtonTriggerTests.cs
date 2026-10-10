using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// Button triggers in the machine (plan 0005, Joel 2026-10-10: Eyeris's loupe chord "hold Right, press Left" in Augram): the
/// pressed button fires at its press while the anchor is held back, the press is then frozen (no hand-back, moves and wheel ticks
/// pass), the chord ends at the first release and fires again when pressed again, another button cancels it, and every swallowed
/// down still gets a swallowed up.
/// </summary>
public sealed class ButtonTriggerTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private const MouseButton Right = MouseButton.Right;
    private const MouseButton Left = MouseButton.Left;

    /// <summary>"Right + Left" as the planner writes it: Right held back, Left joins its press and fires there.</summary>
    private static readonly AnchorPlan Magnifier = AnchorPlan.None.WithAnchor(Right).WithExtras(Right, ownerIsStroke: false, HeldButtons.Left).WithFires(Right, Left);

    private static CaptureStateMachine Machine(int cancelDelayMs = 1000) => new(Stroke, new CaptureThresholds(CancelDelayMs: cancelDelayMs));

    [Fact]
    public void ThePressedButton_FiresAtItsPress_WithWhatThePressHeldBesidesIt()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).After(80).Down(Left).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[0]);
        Assert.Equal(CaptureOutcome.Suppress.Instance, results[1][0]);
        var fired = Assert.IsType<CaptureOutcome.ButtonTrigger>(results[1][1]);
        Assert.Equal(Left, fired.Button);
        Assert.Equal((100, 100), (fired.Start.X, fired.Start.Y));
        Assert.Equal(new PressHold(HeldButtons.Right, Stroke), fired.Hold);
        Assert.Equal(CaptureState.ButtonFiring, machine.State);
    }

    [Fact]
    public void TapAndPressAgain_EndsAtEachRelease_AndFiresAgain_TheAnchorsReleaseReplaysNothing()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).After(60).Up(Left)
            .After(300).Down(Left).After(60).Up(Left).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ButtonTriggerEnded(Left)], results[2]);
        Assert.IsType<CaptureOutcome.ButtonTrigger>(results[3][1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ButtonTriggerEnded(Left)], results[4]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[5]);
        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void TheAnchorReleasedFirst_EndsTheChord_TheFiredButtonsReleaseIsStillSwallowed()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).After(500).Up(Right).Up(Left).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ButtonTriggerEnded(Left)], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void WhileFiring_ThePressIsFrozen_NoHandBack_MovesAndTicksDoNothing_WheelTicksPass()
    {
        var machine = Machine(cancelDelayMs: 300);
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left)
            .After(1000).Tick().Move(400, 400).Wheel(WheelDirection.Up).After(1000).Tick().RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Empty(results[3]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[4]);
        Assert.Empty(results[5]);
        Assert.Equal(CaptureState.ButtonFiring, machine.State);
    }

    [Fact]
    public void AnotherButtonWhileFiring_PassesAndCancels_EndingTheChord()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).Down(MouseButton.X1)
            .Up(MouseButton.X1).Up(Left).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.ButtonTriggerEnded(Left), new CaptureOutcome.Cancelled(CancelReason.OtherButton)], results[2]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[3]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[4]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[5]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void TheAnchorReleasedElsewhereWhileFiring_EndsTheChordAndThePress()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).ReleasedElsewhere(Right).Up(Left).Up(Right).RunOn(machine);

        Assert.Equal([new CaptureOutcome.ButtonTriggerEnded(Left), new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[4]);
    }

    [Fact]
    public void TheFiredButtonReleasedElsewhere_EndsOnlyTheChord()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).ReleasedElsewhere(Left).RunOn(machine);

        Assert.Equal([new CaptureOutcome.ButtonTriggerEnded(Left)], results[2]);
        Assert.Equal(CaptureState.ButtonFiring, machine.State);
    }

    [Fact]
    public void TheAnchorPressedAgainWhileFiring_ItsLostReleaseEndsTheChordFirst()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).Down(Left).Down(Right, 200, 200).RunOn(machine);

        Assert.Equal([new CaptureOutcome.ButtonTriggerEnded(Left), CaptureOutcome.Suppress.Instance], results[2]);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void LeftAfterTheHoldStillHandBack_IsAnOrdinaryClick()
    {
        var machine = Machine(cancelDelayMs: 300);
        var results = new EventScript().WithPlan(Magnifier).Down(Right, 100, 100).After(400).Tick().Down(Left).RunOn(machine);

        Assert.IsType<CaptureOutcome.HandBack>(Assert.Single(results[1]));
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[2]);
    }

    [Fact]
    public void LeftFirstThenRight_FiresNothing()
    {
        var machine = Machine();
        var results = new EventScript().WithPlan(Magnifier).Down(Left, 100, 100).Down(Right).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[0]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[1]);
        Assert.DoesNotContain(results.SelectMany(outcomes => outcomes), outcome => outcome is CaptureOutcome.ButtonTrigger);
    }

    [Fact]
    public void AStrokePress_NeverFires_EvenWhereTheButtonJoinsIt()
    {
        var plan = AnchorPlan.None.WithExtras(Stroke, ownerIsStroke: true, HeldButtons.Left).WithFires(Stroke, Left);
        var machine = Machine();
        var results = new EventScript().WithPlan(plan).Down(Stroke, 100, 100).Down(Left).Up(Left).Up(Stroke).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[1]);
        Assert.IsType<CaptureOutcome.ClickTrigger>(results[3][^1]);
    }

    [Fact]
    public void ThePlanCarriesFiresPerAnchor_BelowTheIgnoreBits()
    {
        var plan = AnchorPlan.None.WithFires(MouseButton.X2, MouseButton.X1);

        Assert.True(plan.Fires(MouseButton.X2, MouseButton.X1));
        Assert.False(plan.Fires(MouseButton.X1, MouseButton.X2));
        Assert.True(plan.FiresAny(MouseButton.X2));
        Assert.False(plan.FiresAny(Right));
        Assert.True(plan.Bits < (1L << 61), "two ignore bits still fit beside the plan in one long");
        Assert.Equal(plan, new AnchorPlan((plan.Bits << 2) >> 2));
    }
}
