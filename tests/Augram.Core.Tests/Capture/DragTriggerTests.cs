using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// Drag triggers in the machine (plan 0015, Photoshop's Right-drag pan): a drag-bound anchor's move past its drag distance fires
/// DragTrigger where a hand-back would come, the press is then frozen until its release (DragTriggerEnded), a click is still
/// replayed, the hold-still time hands nothing back, another button while held hands back as before, and every swallowed down
/// still gets a swallowed up.
/// </summary>
public sealed class DragTriggerTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private const MouseButton Right = MouseButton.Right;
    private const MouseButton Left = MouseButton.Left;

    /// <summary>"Right + drag" as the planner writes it: Right held back, drag-bound, at 10 px.</summary>
    private static readonly AnchorPlan Plan = AnchorPlan.None.WithAnchor(Right);
    private static readonly AnchorDragDistances Drags = AnchorDragDistances.None.WithOwn(Right, 10).WithDragBound(Right);

    private static CaptureStateMachine Machine(int cancelDelayMs = 1000) => new(Stroke, new CaptureThresholds(CancelDelayMs: cancelDelayMs));

    private static EventScript Script() => new EventScript().WithPlan(Plan).WithDrags(Drags);

    [Fact]
    public void AMovePastTheDistance_FiresTheDrag_FromTheStartPoint_WithWhatThePressHeld()
    {
        var machine = Machine();
        var results = Script().Holding(KeyModifiers.Shift).Down(Right, 100, 100).After(20).Move(105, 100).After(20).Move(112, 100).RunOn(machine);

        Assert.Empty(results[1]);
        var fired = Assert.IsType<CaptureOutcome.DragTrigger>(Assert.Single(results[2]));
        Assert.Equal(Right, fired.Button);
        Assert.Equal((100, 100, 112, 100), (fired.Start.X, fired.Start.Y, fired.X, fired.Y));
        Assert.Equal(new PressHold(HeldButtons.Right, Stroke, BeforeKeys: KeyModifiers.Shift), fired.Hold);
        Assert.Equal(CaptureState.DragFiring, machine.State);
    }

    [Fact]
    public void AClickBeforeTheDistance_IsReplayed_AsToday()
    {
        var machine = Machine();
        var results = Script().Down(Right, 100, 100).After(30).Move(104, 100).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Right, 104, 100)], results[2]);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void TheHoldStillTime_HandsNothingBack_ASlowClickReplays_APauseThenADragStillDrags()
    {
        var slow = Machine(cancelDelayMs: 300);
        var click = Script().Down(Right, 100, 100).After(1000).Tick().Up(Right).RunOn(slow);
        Assert.Empty(click[1]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Right, 100, 100)], click[2]);

        var paused = Machine(cancelDelayMs: 300);
        var drag = Script().Down(Right, 100, 100).After(1000).Tick().Move(130, 100).RunOn(paused);
        Assert.Empty(drag[1]);
        Assert.IsType<CaptureOutcome.DragTrigger>(Assert.Single(drag[2]));
    }

    [Fact]
    public void AnotherButtonWhileHeld_HandsBackAsToday_TheDragDoesNotFire()
    {
        var machine = Machine();
        var results = Script().Down(Right, 100, 100).Down(Left).Move(200, 100).Up(Left).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.PassThrough.Instance, new CaptureOutcome.HandBack(Right, new CapturePoint(100, 100, 0), 100, 100)], results[1]);
        Assert.Empty(results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReleaseHandedBack(Right, 200, 100)], results[4]);
    }

    [Fact]
    public void WhileDragging_MovesAndTicksDoNothing_WheelAndOtherButtonsPass_TheDragGoesOn()
    {
        var machine = Machine(cancelDelayMs: 300);
        var results = Script().Down(Right, 100, 100).Move(130, 100).After(1000).Tick().Move(300, 300)
            .Wheel(WheelDirection.Up).Down(Left).Up(Left).RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Empty(results[3]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[4]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[5]);
        Assert.Equal([CaptureOutcome.PassThrough.Instance], results[6]);
        Assert.Equal(CaptureState.DragFiring, machine.State);
    }

    [Fact]
    public void TheButtonsRelease_EndsTheDrag_SwallowedAsItsDownWas_ReplayingNothing()
    {
        var machine = Machine();
        var results = Script().Down(Right, 100, 100).Move(130, 100).After(500).Up(Right).RunOn(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.DragTriggerEnded(Right)], results[2]);
        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void AnotherProgramsRelease_EndsTheDrag_TheRealReleaseStillSwallowed()
    {
        var machine = Machine();
        var results = Script().Down(Right, 100, 100).Move(130, 100).ReleasedElsewhere(Right).Up(Right).RunOn(machine);

        Assert.Equal([new CaptureOutcome.DragTriggerEnded(Right), new CaptureOutcome.Cancelled(CancelReason.ReleasedElsewhere)], results[2]);
        Assert.Equal([CaptureOutcome.Suppress.Instance], results[3]);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }

    [Fact]
    public void TheButtonPressedAgain_AfterAMissedRelease_EndsTheDragFirst()
    {
        var machine = Machine();
        var results = Script().Down(Right, 100, 100).Move(130, 100).After(500).Down(Right, 130, 100).RunOn(machine);

        Assert.Equal([new CaptureOutcome.DragTriggerEnded(Right), CaptureOutcome.Suppress.Instance], results[2]);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void AReset_LeavesTheDrag_ForTheWorkerToRelease()
    {
        var machine = Machine();
        Script().Down(Right, 100, 100).Move(130, 100).RunOn(machine);

        machine.Reset();

        Assert.Equal(CaptureState.Idle, machine.State);
        Assert.Equal(HeldButtons.None, machine.OwedButtons);
    }
}
