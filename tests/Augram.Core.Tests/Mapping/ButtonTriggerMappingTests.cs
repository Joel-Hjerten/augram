using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Button triggers in the mapping (plan 0005): the stored form drops the pressed button from its own set and reads
/// "Right + Left"; the rules want another button than the stroke button held, without the stroke button, and a key as a Remap
/// output; the planner holds the anchor back, takes the pressed button into its press and marks it firing, and lists the Remap
/// outputs the worker holds itself; the resolver matches the press exactly.
/// </summary>
public sealed class ButtonTriggerMappingTests
{
    private const MouseButton Stroke = MouseButton.Middle;

    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));

    private static CommandStep WinShiftX(bool active = true)
        => new(new RemapStep(new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta)), HostPlatform.Windows, active);

    private static PressedTrigger Pressed(MouseButton button, PressHold hold) => new(new Trigger.ButtonTrigger(button), hold);

    [Fact]
    public void TheStoredForm_DropsThePressedButtonFromItsSet_AndReadsRightPlusLeft()
    {
        var trigger = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right | HeldButtons.Left));

        Assert.Equal(HeldButtons.Right, trigger.Hold.Buttons);
        Assert.Equal("Right + Left", trigger.Describe());
        Assert.Equal(trigger, trigger.Normalised());
        Assert.False(trigger.NeedsStroke);
    }

    [Fact]
    public void TwoAreTheSameKind_OnlyForTheSamePressedButton()
    {
        Assert.True(RightLeft.Overlaps(Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right))));
        Assert.False(RightLeft.Overlaps(Trigger.ForButton(MouseButton.X1, new TriggerHold(HeldButtons.Right))));
        Assert.False(RightLeft.Overlaps(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right))));
    }

    [Fact]
    public void APress_MatchesExactly_KeysIncluded()
    {
        Assert.True(Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke)).Matches(RightLeft));
        Assert.False(Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke, AfterKeys: KeyModifiers.Control)).Matches(RightLeft));
        Assert.False(Pressed(MouseButton.X1, new PressHold(HeldButtons.Right, Stroke)).Matches(RightLeft));
        Assert.Equal("Right + Left", Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke)).Describe());
    }

    [Fact]
    public void HoldingTheStrokeButton_IsRefused()
    {
        var magnifier = NewCommand("Magnifier", Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Stroke | HeldButtons.Right)), NewStep("loupe"));

        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(magnifier))));
        Assert.Contains("holds the stroke button", error.Message);
    }

    [Fact]
    public void HoldingNoButton_IsRefused()
    {
        var magnifier = NewCommand("Magnifier", Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.None, KeyModifiers.Control)), NewStep("loupe"));

        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(magnifier))));
        Assert.Contains("holds no button", error.Message);
    }

    [Fact]
    public void ARemapStep_TakesAKeyOutput_NotAButton()
    {
        var key = NewCommand("Magnifier", RightLeft, WinShiftX());
        var button = NewCommand("Middle", RightLeft, new CommandStep(new RemapStep(new RemapOutput.Button(MouseButton.Middle)), HostPlatform.Windows));

        Assert.NotNull(MappingRules.ValidDocument(Document(NewGlobal(key))));
        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(button))));
        Assert.Contains("must be a key", error.Message);
    }

    [Fact]
    public void TheSameChordTwiceInAGroup_IsRefused()
    {
        var first = NewCommand("Magnifier", RightLeft, WinShiftX());
        var second = NewCommand("Back", RightLeft, NewStep("back"));

        var error = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(first, second))));
        Assert.Contains("already uses Right + Left", error.Message);
    }

    [Fact]
    public void ThePlanner_HoldsTheAnchorBack_TakesThePressedButtonIn_AndMarksItFiring()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Magnifier", RightLeft, WinShiftX()))));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke);

        Assert.True(answer.Plan.IsAnchor(MouseButton.Right));
        Assert.True(answer.Plan.Claims(MouseButton.Right, ownerIsStroke: false, MouseButton.Left));
        Assert.True(answer.Plan.Fires(MouseButton.Right, MouseButton.Left));
        var output = Assert.Single(answer.Outputs.Outputs);
        Assert.Equal("Magnifier", output.Name);
        Assert.Equal(new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta), output.Output);
        Assert.Same(output, answer.Outputs.For(Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke))));
        Assert.Null(answer.Outputs.For(Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke, BeforeKeys: KeyModifiers.Alt))));
    }

    [Fact]
    public void ACommandWithOrdinarySteps_FiresButHoldsNoOutput()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Back", RightLeft, NewStep("back")))));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke);

        Assert.True(answer.Plan.Fires(MouseButton.Right, MouseButton.Left));
        Assert.True(answer.Outputs.IsEmpty);
    }

    [Fact]
    public void AnInactiveRemapStep_HoldsNoOutput()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Magnifier", RightLeft, WinShiftX(active: false)))));

        Assert.True(AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke).Outputs.IsEmpty);
    }

    [Fact]
    public void AnAppOverrideToNothing_GivesTheButtonsBackOverThatApp()
    {
        var blender = NewGroup("Blender", null, NewCommand("No magnifier", RightLeft));
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Magnifier", RightLeft, WinShiftX())), blender));

        var answer = AnchorPlanner.Answer(mapping, mapping.Groups.Single(group => group.Name == "Blender"), HostPlatform.Windows, Stroke);

        Assert.False(answer.Plan.IsAnchor(MouseButton.Right));
        Assert.False(answer.Plan.Fires(MouseButton.Right, MouseButton.Left));
        Assert.True(answer.Outputs.IsEmpty);
    }

    [Fact]
    public void ASetNamingThisMachinesStrokeButton_HoldsNothingBackHere()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Magnifier", Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Middle)), WinShiftX()))));

        var answer = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, MouseButton.Middle);

        Assert.True(answer.Plan.IsEmpty);
        Assert.True(answer.Outputs.IsEmpty);
    }

    [Fact]
    public void TheResolver_FiresTheCommandForTheChord()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Back", RightLeft, NewStep("back")))));

        var resolution = CommandResolver.Resolve(mapping, Window(), Pressed(MouseButton.Left, new PressHold(HeldButtons.Right, Stroke)), HostPlatform.Windows);

        Assert.True(resolution.Fires);
        Assert.Equal("Back", resolution.Command!.Name);
    }
}
