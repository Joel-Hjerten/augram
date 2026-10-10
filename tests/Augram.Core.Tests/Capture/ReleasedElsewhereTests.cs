using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// Another program's release of a button (plan 0005 decision 10; Eyeris's loupe chord, log 2026-10-10 19:37): the button is no
/// longer counted as down, a press it owns ends with nothing replayed, fired or released after a hand-back, its real release stays
/// owed, and the event itself never gets a decision.
/// </summary>
public sealed class ReleasedElsewhereTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private const MouseButton Right = MouseButton.Right;

    /// <summary>Right is held back as an anchor (Right + wheel), as over Chrome on Joel's PC.</summary>
    private static readonly AnchorPlan RightAnchor = AnchorPlan.None.WithAnchor(Right);

    [Fact]
    public void ABackgroundButtonReleasedElsewhere_IsNoLongerHeldByTheNextStroke()
    {
        // Over Eyeris Right is no anchor: its down passes. Eyeris swallows the real up and posts its own.
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().Down(Right, 100, 100).ReleasedElsewhere(Right).After(500)
            .Down(Stroke, 300, 300).Move(300, 200).Move(300, 100).Up(Stroke).RunOnForLast(machine);

        var stroke = Assert.IsType<CaptureOutcome.StrokeComplete>(outcomes[^1]);
        Assert.Equal(HeldButtons.None, stroke.Hold.Before);
    }

    [Fact]
    public void WithoutTheReleaseElsewhere_TheStrokeHoldsRight_AsInJoelsLog()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().Down(Right, 100, 100).After(500)
            .Down(Stroke, 300, 300).Move(300, 200).Move(300, 100).Up(Stroke).RunOnForLast(machine);

        var stroke = Assert.IsType<CaptureOutcome.StrokeComplete>(outcomes[^1]);
        Assert.Equal(HeldButtons.Right, stroke.Hold.Before);
    }

    [Fact]
    public void AHeldBackPress_EndsQuietly_AndItsRealReleaseIsStillConsumed()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(RightAnchor).Down(Right, 100, 100).After(100).ReleasedElsewhere(Right).After(100).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[0]);
        Assert.Equal([new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)], results[1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[2]);
        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void AHeldBackPressWhoseRealReleaseNeverComes_LeavesNothingButTheOwedRelease_AndTheNextPressStartsFresh()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(RightAnchor).Down(Right, 100, 100).ReleasedElsewhere(Right).After(2000)
            .Tick().Down(Right, 200, 200).Up(Right).RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Right, 200, 200)], results[4]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void AHandedBackPress_IsNotReleasedAgain_TheOsAlreadyGotTheOtherProgramsRelease()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 300));
        var results = new EventScript().WithPlan(RightAnchor).Down(Right, 100, 100).After(400).Tick()
            .ReleasedElsewhere(Right).After(50).Up(Right).RunOn(machine);

        Assert.IsType<CaptureOutcome.HandBack>(Assert.Single(results[1]));
        Assert.Equal([new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)], results[2]);
        Assert.Null(machine.HandedBackButton);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
    }

    [Fact]
    public void ADrawnStroke_EndsItsTrail_AndIsNeverRecognised()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().Down(Stroke, 100, 100).Move(100, 200).ReleasedElsewhere(Stroke).Up(Stroke).RunOn(machine);

        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal([CaptureOutcome.EndStroke.Instance, new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
    }

    [Fact]
    public void AnotherButtonsReleaseElsewhere_LeavesThePressInProgressAlone()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().Down(Right, 50, 50).Down(Stroke, 100, 100).ReleasedElsewhere(Right).Up(Stroke).RunOn(machine);

        Assert.Empty(results[2]);
        var click = Assert.IsType<CaptureOutcome.ClickTrigger>(results[3][^1]);
        Assert.Equal(HeldButtons.Right, click.Hold.Before);
    }

    [Fact]
    public void IdleWithNothingDown_ItChangesNothing()
    {
        var machine = new CaptureStateMachine(Stroke);

        Assert.Empty(machine.Handle(new CaptureEvent.ButtonReleasedElsewhere(Right, 0, 0, 0)));
        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }
}
