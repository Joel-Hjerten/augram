using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Drag triggers in the mapping (plan 0015, Photoshop's Right-drag pan): "Right + drag" holds exactly one button, any but the
/// stroke button, plus keys; two on the same button and keys clash; a Remap step on one takes a button output, which may hold a
/// key through the press but never a modifier; the planner holds the button back, marks it drag-bound with its distance, and
/// lists the Remap output the worker holds itself.
/// </summary>
public sealed class DragTriggerMappingTests
{
    private const MouseButton Stroke = MouseButton.Middle;

    private static readonly Trigger RightDrag = Trigger.ForDrag(new TriggerHold(HeldButtons.Right));

    private static readonly RemapOutput LeftWithSpace = new RemapOutput.Button(MouseButton.Left) { HeldKey = KeyCode.Space };

    private static CommandStep Remap(RemapOutput output, bool active = true) => new(new RemapStep(output), HostPlatform.Windows, active);

    private static PressedTrigger Pressed(PressHold hold) => new(new Trigger.DragTrigger(), hold);

    private static MappingValidationException Refused(Command command)
        => Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(command))));

    [Fact]
    public void ItReadsRightPlusDrag_WithItsKeys_AndNeedsNoStroke()
    {
        Assert.Equal("Right + drag", RightDrag.Describe());
        Assert.Equal("Shift + Right + drag", Trigger.ForDrag(new TriggerHold(HeldButtons.Right, KeyModifiers.Shift)).Describe(HostPlatform.Windows));
        Assert.Equal("Right + drag", Pressed(new PressHold(HeldButtons.Right, Stroke)).Describe());
        Assert.Equal(RightDrag, RightDrag.Normalised());
        Assert.False(RightDrag.NeedsStroke);
    }

    [Fact]
    public void APress_MatchesExactly_KeysIncluded()
    {
        Assert.True(Pressed(new PressHold(HeldButtons.Right, Stroke)).Matches(RightDrag));
        Assert.False(Pressed(new PressHold(HeldButtons.Right, Stroke, BeforeKeys: KeyModifiers.Shift)).Matches(RightDrag));
        Assert.False(Pressed(new PressHold(HeldButtons.Left, Stroke)).Matches(RightDrag));
        Assert.False(new PressedTrigger(new Trigger.ButtonTrigger(MouseButton.Left), new PressHold(HeldButtons.Right, Stroke)).Matches(RightDrag));
    }

    [Fact]
    public void AnyButtonButTheStrokeButton_MayBeDragged_LeftIncluded()
    {
        var pan = NewCommand("Pan", Trigger.ForDrag(new TriggerHold(HeldButtons.Left)), Remap(LeftWithSpace));

        Assert.NotNull(MappingRules.ValidDocument(Document(NewGlobal(pan))));
    }

    [Fact]
    public void HoldingTheStrokeButton_NoButton_OrTwoButtons_IsRefused()
    {
        Assert.Contains("holds the stroke button", Refused(NewCommand("Pan", Trigger.ForDrag(new TriggerHold(HeldButtons.Stroke | HeldButtons.Right)), NewStep("pan"))).Message);
        Assert.Contains("must hold exactly one button", Refused(NewCommand("Pan", Trigger.ForDrag(new TriggerHold(HeldButtons.None, KeyModifiers.Shift)), NewStep("pan"))).Message);
        Assert.Contains("must hold exactly one button", Refused(NewCommand("Pan", Trigger.ForDrag(new TriggerHold(HeldButtons.Right | HeldButtons.Left)), NewStep("pan"))).Message);
    }

    [Fact]
    public void TheSameDragTwiceInAGroup_IsRefused_OtherKeysOrAnotherKindAreNot()
    {
        var first = NewCommand("Pan", RightDrag, Remap(LeftWithSpace));
        var shifted = NewCommand("Zoom", Trigger.ForDrag(new TriggerHold(HeldButtons.Right, KeyModifiers.Shift)), NewStep("zoom"));
        var wheel = NewCommand("Wheel", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("wheel"));

        Assert.NotNull(MappingRules.ValidDocument(Document(NewGlobal(first, shifted, wheel))));
        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(first, NewCommand("Back", RightDrag, NewStep("back"))))));
        Assert.Contains("already uses Right + drag", error.Message);
    }

    [Fact]
    public void ARemapStep_TakesAButtonOutput_NotAKey()
    {
        var error = Refused(NewCommand("Pan", RightDrag, Remap(new RemapOutput.Key(KeyCode.Space))));

        Assert.Equal("'Pan' has a drag trigger: its Remap output must be a button.", error.Message);
    }

    [Fact]
    public void AModifierHeldThroughThePress_IsRefused_InTheInputKeysWords()
    {
        var output = new RemapOutput.Button(MouseButton.Left) { HeldKey = KeyCode.LeftShift };

        var error = Refused(NewCommand("Pan", RightDrag, Remap(output)));

        Assert.Equal("'Pan' holds Left Shift through its Remap press: Ctrl, Alt, Shift and Win are the output's modifiers.", error.Message);
        Assert.Equal("Left Shift cannot be held through the press: Ctrl, Alt, Shift and Win are the output's modifiers.", HoldRemapRules.HeldKeyProblem(KeyCode.LeftShift, HostPlatform.Windows));
        Assert.Null(HoldRemapRules.HeldKeyProblem(KeyCode.Space, HostPlatform.Windows));
        Assert.Null(HoldRemapRules.HeldKeyProblem(KeyCode.None, HostPlatform.Windows));
    }

    [Fact]
    public void ARemapStepFittedToADrag_IsALeftButton_ItsModifiersKept()
    {
        var fitted = HoldRemapRules.FittedTo(new RemapStep(new RemapOutput.Key(KeyCode.X, KeyModifiers.Control)), RightDrag);
        var kept = HoldRemapRules.FittedTo(new RemapStep(LeftWithSpace), RightDrag);

        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Left, KeyModifiers.Control)), fitted);
        Assert.Equal(new RemapStep(LeftWithSpace), kept);
    }

    [Fact]
    public void TheStepOffer_OffersRemapOnADrag_AsALeftButton()
    {
        var pan = NewCommand("Pan", RightDrag);

        Assert.Null(StepOffer.Check(RemapStepType.Instance, pan, HostPlatform.Windows));
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Left)), StepOffer.NewStep(RemapStepType.Instance, pan, HostPlatform.Windows));
    }

    [Fact]
    public void ThePlanner_HoldsTheButtonBack_MarksItDragBound_WithItsDistance_AndListsTheOutput()
    {
        var pan = NewCommand("Pan", Trigger.ForDrag(new TriggerHold(HeldButtons.Right, DragDistancePx: 6)), Remap(LeftWithSpace));
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(pan)));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke);

        Assert.True(answer.Plan.IsAnchor(MouseButton.Right));
        Assert.Equal(HeldButtons.None, answer.Plan.ExtrasFor(MouseButton.Right, ownerIsStroke: false));
        Assert.True(answer.Drags.IsDragBound(MouseButton.Right));
        Assert.False(answer.Drags.IsDragBound(MouseButton.Left));
        Assert.Equal(6, answer.Drags.For(MouseButton.Right, 10));
        var output = Assert.Single(answer.Outputs.Outputs);
        Assert.Equal(LeftWithSpace, output.Output);
        Assert.Same(output, answer.Outputs.For(Pressed(new PressHold(HeldButtons.Right, Stroke))));
    }

    [Fact]
    public void OrdinarySteps_BindTheDrag_HoldNoOutput_AndRunStepsForTheirKeysOnly()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Back", RightDrag, NewStep("back")))));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke);

        Assert.True(answer.Drags.IsDragBound(MouseButton.Right));
        Assert.Empty(answer.Outputs.Outputs);
        Assert.True(answer.Outputs.RunsSteps(Pressed(new PressHold(HeldButtons.Right, Stroke))));
        Assert.False(answer.Outputs.RunsSteps(Pressed(new PressHold(HeldButtons.Right, Stroke, BeforeKeys: KeyModifiers.Shift))), "no command takes Shift + Right + drag: the press is handed back");
        Assert.False(answer.Outputs.RunsSteps(new PressedTrigger(new Trigger.ButtonTrigger(MouseButton.Left), new PressHold(HeldButtons.Right, Stroke))));
    }

    [Fact]
    public void ADragOfThisMachinesStrokeButton_HoldsNothingBack()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Pan", RightDrag, Remap(LeftWithSpace)))));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, MouseButton.Right);

        Assert.Equal(AnchorPlan.None, answer.Plan);
        Assert.False(answer.Drags.IsDragBound(MouseButton.Right));
        Assert.True(answer.Outputs.IsEmpty);
    }

    [Fact]
    public void TheHeldKey_IsInTheSummary()
    {
        Assert.Equal("Remap to Left with Space held", new RemapStep(LeftWithSpace).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Remap to Ctrl + Left with Space held", new RemapStep(new RemapOutput.Button(MouseButton.Left, KeyModifiers.Control) { HeldKey = KeyCode.Space }).SummaryOn(HostPlatform.Windows));
    }
}
