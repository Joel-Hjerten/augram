using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Capture;

/// <summary>
/// A command's own button drag distance (Joel, 2026-10-10, plan 0004): how far a press of a button other than the stroke button
/// may move before it is handed back to the app as a drag. The press is decided before the wheel says which command it is, so
/// over one window the largest distance of the commands holding that button wins, a command without its own counting as the
/// Options value.
/// </summary>
public sealed class DragDistanceTests
{
    private const MouseButton Stroke = MouseButton.Middle;

    private static Trigger RightWheel(WheelDirection direction, int? distance = null)
        => Trigger.ForWheel(direction, new TriggerHold(HeldButtons.Right, DragDistancePx: distance));

    [Fact]
    public void NoInformation_UsesTheOptionsValue()
    {
        Assert.Equal(10, AnchorDragDistances.None.For(MouseButton.Right, 10));
    }

    [Fact]
    public void OwnDistancesKeepTheLargest_PerButton_AndTheOptionsValueCountsWhereACommandSetsNone()
    {
        var drags = AnchorDragDistances.None.WithOwn(MouseButton.Right, 4).WithOwn(MouseButton.Right, 3).WithOwn(MouseButton.X1, 200);

        Assert.Equal(4, drags.For(MouseButton.Right, 10));
        Assert.Equal(200, drags.For(MouseButton.X1, 10));
        Assert.Equal(10, drags.For(MouseButton.Left, 10));
        Assert.Equal(10, drags.WithOptionsValue(MouseButton.Right).For(MouseButton.Right, 10));
        Assert.Equal(12, drags.WithOptionsValue(MouseButton.Right).WithOwn(MouseButton.Right, 12).For(MouseButton.Right, 10));
    }

    [Fact]
    public void TheStoredHold_KeepsADistanceOnlyWhereItsAnchorsAreHandedBack()
    {
        Assert.Equal(4, Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right, DragDistancePx: 4)).Hold.DragDistancePx);
        Assert.Null(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Stroke | HeldButtons.Right, DragDistancePx: 4)).Hold.DragDistancePx);
        Assert.Null(Trigger.ForGesture(Up, new TriggerHold(HeldButtons.Right, DragDistancePx: 4)).Hold.DragDistancePx);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void ADistanceOutOfRange_IsRefused(int distance)
    {
        var zoom = NewCommand("Zoom In", RightWheel(WheelDirection.Down, distance), NewStep("zoom"));

        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(zoom))));
        Assert.Contains("between 1 and 200 px", error.Message);
    }

    [Fact]
    public void ThePlanner_GivesEachAnchorTheLargestDistanceOfItsCommandsOverTheWindow()
    {
        var zoomIn = NewCommand("Zoom In", RightWheel(WheelDirection.Down, 4), NewStep("in"));
        var zoomOut = NewCommand("Zoom Out", RightWheel(WheelDirection.Up, 6), NewStep("out"));
        var spineOnly = NewGroup("Spine", null, NewCommand("Pan less", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.X1)), NewStep("x")));
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(zoomIn, zoomOut), spineOnly));

        var (plan, drags) = AnchorPlanner.AnswerForGroup(mapping, null, HostPlatform.Windows, Stroke);
        Assert.True(plan.IsAnchor(MouseButton.Right));
        Assert.Equal(6, drags.For(MouseButton.Right, 10));

        var overSpine = AnchorPlanner.AnswerForGroup(mapping, mapping.Groups.Single(group => group.Name == "Spine"), HostPlatform.Windows, Stroke).Drags;
        Assert.Equal(6, overSpine.For(MouseButton.Right, 10));
        Assert.Equal(25, overSpine.For(MouseButton.X1, 25));
    }

    [Fact]
    public void ACommandWithoutItsOwn_CountsAsTheOptionsValue()
    {
        var zoomIn = NewCommand("Zoom In", RightWheel(WheelDirection.Down, 4), NewStep("in"));
        var zoomOut = NewCommand("Zoom Out", RightWheel(WheelDirection.Up), NewStep("out"));
        var drags = AnchorPlanner.AnswerForGroup(MappingRules.ValidDocument(Document(NewGlobal(zoomIn, zoomOut))), null, HostPlatform.Windows, Stroke).Drags;

        Assert.Equal(10, drags.For(MouseButton.Right, 10));
        Assert.Equal(4, drags.For(MouseButton.Right, 2));
    }

    [Fact]
    public void TheMachine_HandsBackAtThePressesDistance_NotTheOptionsValue()
    {
        var plan = AnchorPlan.None.WithAnchor(MouseButton.Right);
        var drags = AnchorDragDistances.None.WithOwn(MouseButton.Right, 3);
        var machine = new CaptureStateMachine(Stroke, new CaptureThresholds(ButtonDragDistancePx: 20));

        Assert.Equal([CaptureOutcome.Suppress.Instance], machine.Handle(new CaptureEvent.ButtonDown(MouseButton.Right, 100, 100, 0, Plan: plan, Drags: drags)));
        Assert.Empty(machine.Handle(new CaptureEvent.Move(102, 100, 5)));
        Assert.Equal(
            [new CaptureOutcome.HandBack(MouseButton.Right, new CapturePoint(100, 100, 0), 103, 100)],
            machine.Handle(new CaptureEvent.Move(103, 100, 10)));
    }
}
