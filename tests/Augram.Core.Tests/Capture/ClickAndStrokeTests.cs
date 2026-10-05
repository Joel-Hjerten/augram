using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Capture;

/// <summary>The happy paths of the contract table: press, click replay, stroke begin, decimation, stroke complete.</summary>
public sealed class ClickAndStrokeTests
{
    private const MouseButton Stroke = MouseButton.Right;

    [Fact]
    public void StrokeButtonDownWhileIdle_IsSuppressed_AndGoesHeld()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance], outcomes);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void MoveUnderStartDistanceWhileHeld_EmitsNothing_AndStaysHeld()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(120, 100).RunOnForLast(machine);

        Assert.Empty(outcomes);
        Assert.Equal(CaptureState.Held, machine.State);
    }

    [Fact]
    public void MoveReachingStartDistance_BeginsStroke_WithTheRecordedPointsSoFar()
    {
        var machine = new CaptureStateMachine(Stroke);
        var script = new EventScript().Down(Stroke, 100, 100).Move(110, 100).After(5).Move(120, 100).After(5).Move(130, 100);

        var outcomes = script.RunOnForLast(machine);

        Assert.Equal(
            [
                new CaptureOutcome.BeginStroke(new CapturePoint(100, 100, 0)),
                new CaptureOutcome.StrokeProgress(new CapturePoint(110, 100, 0)),
                new CaptureOutcome.StrokeProgress(new CapturePoint(120, 100, 5)),
                new CaptureOutcome.StrokeProgress(new CapturePoint(130, 100, 10)),
            ],
            outcomes);
        Assert.Equal(CaptureState.Drawing, machine.State);
    }

    [Fact]
    public void MoveWhileDrawing_IsDecimated_ByMinSegment()
    {
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(MinSegmentPx: 6));
        var script = new EventScript().Down(Stroke, 100, 100).Move(130, 100)
            .Move(133, 100)   // 3 px from last recorded: dropped
            .Move(135, 100)   // 5 px: dropped
            .Move(136, 100)   // 6 px: recorded
            .Move(140, 103);  // 5 px: dropped

        var results = script.RunOn(machine);

        Assert.Empty(results[2]);
        Assert.Empty(results[3]);
        Assert.Equal([new CaptureOutcome.StrokeProgress(new CapturePoint(136, 100, 0))], results[4]);
        Assert.Empty(results[5]);
    }

    [Fact]
    public void StrokeButtonUpWhileHeld_ReplaysClickAtReleasePosition_AndGoesIdle()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(105, 102).Up(Stroke).RunOnForLast(machine);

        Assert.Equal([CaptureOutcome.Suppress.Instance, new CaptureOutcome.ReplayClick(Stroke, 105, 102)], outcomes);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void StrokeButtonUpWhileDrawing_CompletesStroke_WithStartAndReleasePoints()
    {
        var machine = new CaptureStateMachine(Stroke);
        var script = new EventScript().Down(Stroke, 100, 100).After(10).Move(140, 100).After(10).Move(180, 100).After(10).Up(Stroke, 182, 101);

        var outcomes = script.RunOnForLast(machine);

        Assert.Equal(3, outcomes.Count);
        Assert.Equal(CaptureOutcome.Suppress.Instance, outcomes[0]);
        Assert.Equal(CaptureOutcome.EndStroke.Instance, outcomes[1]);
        var complete = Assert.IsType<CaptureOutcome.StrokeComplete>(outcomes[2]);
        Assert.Equal(Stroke, complete.Button);
        Assert.Equal(new CapturePoint(100, 100, 0), complete.Start);
        Assert.Equal(
            [new CapturePoint(100, 100, 0), new CapturePoint(140, 100, 10), new CapturePoint(180, 100, 20), new CapturePoint(182, 101, 30)],
            complete.Points);
        Assert.Equal(CaptureState.Idle, machine.State);
    }

    [Fact]
    public void ReleaseAtLastRecordedPoint_IsNotDuplicated()
    {
        var machine = new CaptureStateMachine(Stroke);

        var outcomes = new EventScript().Down(Stroke, 100, 100).Move(140, 100).Up(Stroke).RunOnForLast(machine);

        var complete = Assert.IsType<CaptureOutcome.StrokeComplete>(outcomes[^1]);
        Assert.Equal([new CapturePoint(100, 100, 0), new CapturePoint(140, 100, 0)], complete.Points);
    }

    [Fact]
    public void CompletedStrokePoints_AreNotMutatedByTheNextCapture()
    {
        var machine = new CaptureStateMachine(Stroke);
        var first = new EventScript().Down(Stroke, 100, 100).Move(140, 100).Up(Stroke).RunOnForLast(machine);
        var points = Assert.IsType<CaptureOutcome.StrokeComplete>(first[^1]).Points;

        new EventScript().Down(Stroke, 0, 0).Move(50, 0).Move(100, 0).Up(Stroke).RunOn(machine);

        Assert.Equal([new CapturePoint(100, 100, 0), new CapturePoint(140, 100, 0)], points);
    }

    [Fact]
    public void ThresholdsSwap_TakesEffectOnTheNextEvent()
    {
        var machine = new CaptureStateMachine(Stroke);
        new EventScript().Down(Stroke, 100, 100).Move(110, 100).RunOn(machine);

        machine.Thresholds = new CaptureThresholds(StartDistancePx: 10);
        var outcomes = machine.Handle(new CaptureEvent.Move(111, 100, 0));

        Assert.Equal(CaptureState.Drawing, machine.State);
        Assert.Equal(new CaptureOutcome.BeginStroke(new CapturePoint(100, 100, 0)), outcomes[0]);
    }
}
