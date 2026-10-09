using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>The rules of plan 0002's Core table (<see cref="HoldRemapRules"/>, run through <see cref="MappingRules.ValidDocument"/>).</summary>
public sealed class HoldRemapRulesTests
{
    [Fact]
    public void JoelsBlenderSetupIsValid_HoldRemapsSortedByName_UnnamedOnesNamedAfterTheirKey()
    {
        var space = NewSpace();
        var unnamed = new HoldRemap(HoldRemapId.New(), "  ", KeyCode.S);
        var valid = Blender.Document(Group(space) with { HoldRemaps = [space, unnamed] });

        Assert.Equal(["S", "Space"], valid.Groups[1].HoldRemaps.Select(holdRemap => holdRemap.Name));
        Assert.Equal(10, valid.Groups[1].Commands.Count(command => command.HoldRemapId == space.Id));
    }

    [Theory]
    [InlineData(KeyCode.LeftControl, "Left Ctrl")]
    [InlineData(KeyCode.RightAlt, "Right Alt")]
    [InlineData(KeyCode.LeftShift, "Left Shift")]
    [InlineData(KeyCode.LeftMeta, "Left Win")]
    public void AModifierCannotBeAHoldKey(KeyCode key, string name)
    {
        var ex = Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(key)] });

        Assert.Equal($"{name} cannot be a hold key: Ctrl, Alt, Shift and Win are already held for triggers.", ex.Message);
    }

    [Fact]
    public void HoldKeysAndNamesAreUniquePerGroup_ButTwoGroupsMayBothHoldSpace()
    {
        var sameKey = Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space), HoldRemap.For(KeyCode.Space) with { Name = "Navigate" }] });
        var sameName = Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space), HoldRemap.For(KeyCode.S) with { Name = "SPACE" }] });

        Assert.Equal("'Navigate' in 'Blender' already uses Space as its hold key.", sameKey.Message);
        Assert.Equal("A hold remap named 'Space' already exists in 'Blender'.", sameName.Message);
        var both = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space)] }, NewGroup("Maya") with { HoldRemaps = [HoldRemap.For(KeyCode.Space)] }));
        Assert.Equal(2, both.Groups.Sum(group => group.HoldRemaps.Count));
    }

    [Fact]
    public void HoldRemapsWithoutAKeyYetMayRepeat()
    {
        var valid = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.None), HoldRemap.For(KeyCode.None) with { Name = "Other" }] }));

        Assert.Equal(["Hold remap", "Other"], valid.Groups[1].HoldRemaps.Select(holdRemap => holdRemap.Name));
    }

    [Fact]
    public void TheTapTimeStaysWithinRange_UseOnSomewhere_NeverOnGlobal()
    {
        Assert.Equal("The tap time of 'Space' must be between 0 and 2000 ms.", Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space) with { TapTimeMs = 2001 }] }).Message);
        Assert.Equal("The tap time of 'Space' must be between 0 and 2000 ms.", Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space) with { TapTimeMs = -1 }] }).Message);
        Assert.Equal("Use 'Space' on at least one platform.", Refused(NewGroup("Blender") with { HoldRemaps = [HoldRemap.For(KeyCode.Space) with { UseOn = PlatformSet.None }] }).Message);
        var global = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Mapping.Support.MappingFixtures.Document(NewGlobal() with { HoldRemaps = [HoldRemap.For(KeyCode.Space)] })));
        Assert.Equal("The Global group cannot have hold remaps: add 'Space' to an app group.", global.Message);
    }

    [Fact]
    public void AnInputIsUniquePerHoldRemap_TwoHoldRemapsMayBothUseLeft()
    {
        var space = NewSpace();
        var s = HoldRemap.For(KeyCode.S);
        var twice = Refused(NewGroup("Blender", null, Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle), Remap(space, "Spin", HoldInput.Of(MouseButton.Left), CtrlMiddle)) with { HoldRemaps = [space] });

        Assert.Equal("'Orbit' under 'Space' in 'Blender' already uses Left.", twice.Message);
        var valid = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender", null, Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle), Remap(s, "Spin", HoldInput.Of(MouseButton.Left), CtrlMiddle)) with { HoldRemaps = [space, s] }));
        Assert.Equal(2, valid.Groups[1].Commands.Count);
    }

    [Fact]
    public void ButtonSetsMatchExactly_LeftAndLeftPlusRightAreDifferentInputs()
    {
        var valid = Blender.Document();

        Assert.Equal(2, valid.Groups[1].Commands.Count(command => command.Trigger is Trigger.InputTrigger { Input: HoldInput.Buttons { Set: var set } } && set.Has(MouseButton.Left)));
    }

    [Fact]
    public void AnInputOnlyUnderAHoldRemap_AndOnlyAnInputThere()
    {
        var space = NewSpace();
        var ordinary = Refused(NewGroup("Blender", null, NewCommand("Orbit", Trigger.ForInput(HoldInput.Of(MouseButton.Left)))) with { HoldRemaps = [space] });
        var gesture = Refused(NewGroup("Blender", null, NewCommand("Close", Up) with { HoldRemapId = space.Id }) with { HoldRemaps = [space] });

        Assert.Equal("'Orbit' has the input Left but is not under a hold remap: only a command under one has an input.", ordinary.Message);
        Assert.Equal("'Close' is under the hold remap 'Space': its trigger must be an input (a button, buttons held together, a wheel direction or a key), not this gesture.", gesture.Message);
        var unbound = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender", null, NewCommand("Later") with { HoldRemapId = space.Id }) with { HoldRemaps = [space] }));
        Assert.Equal(space.Id, unbound.Groups[1].Commands[0].HoldRemapId);
    }

    [Fact]
    public void AnInputNamesAButtonOrAKey_NeverAModifierOrTheHoldKey()
    {
        var space = NewSpace();

        Assert.Equal("'Nothing' needs at least one button as its input.", Refused(Under(space, Remap(space, "Nothing", new HoldInput.Buttons(HeldButtons.Stroke), Middle))).Message);
        Assert.Equal("'Nothing' needs a key as its input.", Refused(Under(space, Remap(space, "Nothing", new HoldInput.Key(KeyCode.None), G))).Message);
        Assert.Equal("'Shifty' cannot use Left Shift as its input: Ctrl, Alt, Shift and Win pass through while a hold key is held.", Refused(Under(space, Remap(space, "Shifty", new HoldInput.Key(KeyCode.LeftShift), G))).Message);
        Assert.Equal("'Loop' cannot use Space as its input: it is the hold key of 'Space'.", Refused(Under(space, Remap(space, "Loop", new HoldInput.Key(KeyCode.Space), G))).Message);
    }

    [Fact]
    public void ARemapStepIsACommandsOnlyStep()
    {
        var space = NewSpace();
        var both = Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle);
        both = both with { Steps = [.. both.Steps, NewStep("more")] };

        Assert.Equal("'Orbit' has a Remap step among other steps: a Remap step is a command's only step.", Refused(Under(space, both)).Message);
    }

    [Fact]
    public void AWheelInputTakesAKeyOrAWheelOutput_AWheelOutputNeedsAWheelInput()
    {
        var space = NewSpace();
        var up = new HoldInput.Wheel(WheelDirection.Up);

        Assert.Equal("'Zoom' turns the wheel: its Remap output must be a key or a wheel notch, not a button.", Refused(Under(space, Remap(space, "Zoom", up, Middle))).Message);
        Assert.Equal("'Scroll' sends a wheel notch: that output is for a wheel input only.", Refused(Under(space, Remap(space, "Scroll", HoldInput.Of(MouseButton.Left), new RemapOutput.Wheel(ScrollDirection.Up)))).Message);
        var valid = MappingRules.ValidDocument(Document(NewGlobal(), Under(space,
            Remap(space, "Zoom in", up, new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control)),
            Remap(space, "Next", new HoldInput.Wheel(WheelDirection.Down), new RemapOutput.Key(KeyCode.PageDown)))));
        Assert.Equal(2, valid.Groups[1].Commands.Count);
    }

    [Fact]
    public void ACommandUnderAHoldRemapTheGroupLacksBecomesOrdinary_ItsInputCleared_NeverRefused()
    {
        var gone = NewSpace();
        var orbit = Remap(gone, "Orbit", HoldInput.Of(MouseButton.Left), Middle);

        var valid = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender", null, orbit)));

        var command = valid.Groups[1].Commands.Single();
        Assert.Null(command.HoldRemapId);
        Assert.Same(Trigger.None, command.Trigger);
        Assert.IsType<RemapStep>(command.Steps.Single().Step);
    }

    [Fact]
    public void ACommandUnderAHoldRemapHasNoCategory()
    {
        var space = NewSpace();
        var media = NewCategory("Media");
        var orbit = Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle).In(media);

        var valid = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Blender", null, orbit) with { HoldRemaps = [space], Categories = [media] }));

        Assert.Null(valid.Groups[1].Commands.Single().CategoryId);
    }

    private static AppGroup Under(HoldRemap holdRemap, params Command[] commands) => NewGroup("Blender", null, commands) with { HoldRemaps = [holdRemap] };

    private static MappingValidationException Refused(AppGroup group)
        => Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), group)));
}
