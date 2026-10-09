using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// Trigger combinations in the machine (F1, Joel 2026-10-09; learnings 0003 §4.2 rows): what a press records as held Before
/// and After, buttons the window's plan takes into a press, anchors other than the stroke button (click replayed at release,
/// handed back on a move, after the hold-still time or when another button goes down), and that every swallowed down gets a
/// swallowed up.
/// </summary>
public sealed class ChordTests
{
    private const MouseButton Stroke = MouseButton.Middle;

    /// <summary>Right is held back as an anchor (Right + wheel); Left joins a stroke-button press (stroke + Left).</summary>
    private static readonly AnchorPlan Plan = AnchorPlan.None
        .WithAnchor(MouseButton.Right)
        .WithExtras(Stroke, ownerIsStroke: true, HeldButtons.Left);

    [Fact]
    public void AButtonThePlanClaims_JoinsTheStrokePress_ItsClickNeverReachesTheApp()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(Stroke, 100, 100).Down(MouseButton.Left).Up(MouseButton.Left).Up(Stroke).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[2]);
        var click = Assert.IsType<CaptureOutcome.ClickTrigger>(results[3][^1]);
        Assert.Equal(new PressHold(HeldButtons.Stroke, Stroke, After: HeldButtons.Left), click.Hold);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void AButtonNoCommandTakes_PassesAndCancels_AsBefore()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().WithPlan(Plan).Down(Stroke, 100, 100).Down(MouseButton.X1).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.Cancelled(CancelReason.OtherButton)], outcomes);
    }

    [Fact]
    public void KeysHeldAtThePress_AreBefore_KeysPressedDuring_AreAfter()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().Holding(KeyModifiers.Shift).Down(Stroke, 100, 100).Key(KeyModifiers.Control).Key(KeyModifiers.Shift).Key(KeyModifiers.Alt, consumed: false).Up(Stroke).RunOnForLast(machine);

        var click = Assert.IsType<CaptureOutcome.ClickTrigger>(outcomes[^1]);
        Assert.Equal(KeyModifiers.Shift, click.Hold.BeforeKeys);
        Assert.Equal(KeyModifiers.Control, click.Hold.AfterKeys);
    }

    [Fact]
    public void APlainClick_IsStillReplayed()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().WithPlan(Plan).Down(Stroke, 100, 100).Up(Stroke).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Stroke, 100, 100)], outcomes);
    }

    [Fact]
    public void AnAnchorClick_IsReplayedAtRelease_WithItsAfterKeysAroundIt()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).Key(KeyModifiers.Shift).After(50).Up(MouseButton.Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], results[0]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(MouseButton.Right, 100, 100) { AfterKeys = KeyModifiers.Shift }], results[2]);
    }

    [Fact]
    public void AButtonThatIsNoAnchorHere_IsUntouched()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().Down(MouseButton.Right, 100, 100).Wheel(WheelDirection.Up).Up(MouseButton.Right).RunOn(machine);

        Assert.All(results, outcomes => Assert.Equal([CaptureOutcome.PassThrough.Instance], outcomes));
    }

    [Fact]
    public void AnAnchorAndTheWheel_FireWithThatAnchor_AndTheReleaseIsSwallowed()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).Wheel(WheelDirection.Up).Wheel(WheelDirection.Up).Up(MouseButton.Right).RunOn(machine);

        var tick = Assert.IsType<CaptureOutcome.WheelTrigger>(results[1][^1]);
        Assert.Equal(new PressHold(HeldButtons.Right, Stroke), tick.Hold);
        Assert.False(tick.AfterDrawing);
        Assert.IsType<CaptureOutcome.WheelTrigger>(results[2][^1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
    }

    [Fact]
    public void AnAnchorDragged_IsHandedBackAtOnce_AndItsReleaseIsInjected()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).Move(110, 100).Move(140, 100).Move(200, 100).Up(MouseButton.Right).RunOn(machine);

        Assert.Empty(results[1]);
        Assert.Equal([new CaptureOutcome.HandBack(MouseButton.Right, new CapturePoint(100, 100, 0), 140, 100)], results[2]);
        Assert.Empty(results[3]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReleaseHandedBack(MouseButton.Right, 200, 100)], results[4]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void AnAnchorHeldStill_IsHandedBackAfterTheHoldStillTime()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 500));
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).After(499).Tick().After(1).Tick().RunOn(machine);

        Assert.Empty(results[1]);
        Assert.Equal([new CaptureOutcome.HandBack(MouseButton.Right, new CapturePoint(100, 100, 0), 100, 100)], results[2]);
        Assert.Equal(MouseButton.Right, machine.HandedBackButton);
    }

    [Fact]
    public void AnotherButtonDuringAnAnchor_PassesAndHandsTheAnchorBack()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).Down(MouseButton.Left).Up(MouseButton.Left).Up(MouseButton.Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.HandBack(MouseButton.Right, new CapturePoint(100, 100, 0), 100, 100)], results[1]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReleaseHandedBack(MouseButton.Right, 100, 100)], results[3]);
    }

    [Fact]
    public void AnAfterButtonReleasedAfterItsOwner_IsStillSwallowed()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(Stroke, 100, 100).Down(MouseButton.Left).Up(Stroke).Up(MouseButton.Left).RunOn(machine);

        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void AButtonHeldBeforeThePress_IsBefore_AndItsClickReachedTheApp()
    {
        var machine = new CaptureStateMachine(Stroke);
        var results = new EventScript().WithPlan(Plan).Down(MouseButton.Left, 100, 100).Down(Stroke).Up(Stroke).Up(MouseButton.Left).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[0]);
        Assert.Equal(new PressHold(HeldButtons.Stroke, Stroke, Before: HeldButtons.Left), Assert.IsType<CaptureOutcome.ClickTrigger>(results[2][^1]).Hold);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[3]);
    }

    [Fact]
    public void AWheelTickAfterDrawing_IsMarked_SoNoWheelCommandFires()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(150, 100).Wheel(WheelDirection.Up).Wheel(WheelDirection.Up).RunOnForLast(machine);

        Assert.True(Assert.IsType<CaptureOutcome.WheelTrigger>(outcomes[^1]).AfterDrawing);
    }

    [Fact]
    public void AnAfterKeyOrButton_PushesTheHoldStillDeadline()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(CancelDelayMs: 500));
        var results = new EventScript().WithPlan(Plan).Down(Stroke, 100, 100).After(400).Key(KeyModifiers.Control).After(400).Tick().After(100).Tick().RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Equal([new CaptureOutcome.Cancelled(CancelReason.HoldStill)], results[3]);
    }

    [Fact]
    public void TheOwnerPressedAgainAfterAHandBack_ReleasesTheInjectedDownFirst()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().WithPlan(Plan).Down(MouseButton.Right, 100, 100).Move(200, 100).Down(MouseButton.Right, 300, 100).RunOnForLast(machine);

        Assert.Equal([new CaptureOutcome.ReleaseHandedBack(MouseButton.Right, 300, 100), CaptureOutcome.Suppress.Instance], outcomes);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void AKeyAfterTheFirstTick_IsNotRecorded()
    {
        var machine = new CaptureStateMachine(Stroke);
        var outcomes = new EventScript().Down(Stroke, 100, 100).Wheel(WheelDirection.Up).Key(KeyModifiers.Shift).Wheel(WheelDirection.Up).RunOnForLast(machine);

        Assert.Equal(KeyModifiers.None, Assert.IsType<CaptureOutcome.WheelTrigger>(outcomes[^1]).Hold.AfterKeys);
    }
}
