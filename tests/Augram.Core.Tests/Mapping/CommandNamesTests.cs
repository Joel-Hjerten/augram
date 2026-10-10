using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Command names are unique within their parent (Joel, 2026-10-10): the hold remap a command sits under, or its group's
/// ordinary commands (<see cref="CommandNames"/>, run by <see cref="MappingRules"/>); and how a command is named for the user.
/// </summary>
public sealed class CommandNamesTests
{
    [Fact]
    public void TwoOrbitsUnderTwoHoldRemapsOfOneGroupAreFine_AndSoIsAnOrdinaryOrbitBesideThem()
    {
        var space = NewSpace();
        var s = HoldRemap.For(KeyCode.S);

        var valid = MappingRules.ValidDocument(Document(NewGlobal(), BlenderWith(
            [space, s],
            Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle),
            Remap(s, "Orbit", HoldInput.Of(MouseButton.Left), Middle),
            NewCommand("orbit", Up))));

        Assert.Equal(3, valid.Groups[1].Commands.Count(command => MappingRules.NameComparer.Equals(command.Name, "Orbit")));
    }

    [Fact]
    public void TwoOrbitsUnderOneHoldRemapAreRefused_NamingTheParent()
    {
        var space = NewSpace();

        var twice = Refused(BlenderWith(
            [space],
            Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle),
            Remap(space, "ORBIT", HoldInput.Of(MouseButton.Right), Middle)));

        Assert.Equal("A command named 'Orbit' already exists under 'Space' in 'Blender'.", twice.Message);
    }

    [Fact]
    public void TwoOrdinaryOrbitsAreRefusedAsBefore()
    {
        var ordinary = Refused(BlenderWith([NewSpace()], NewCommand("Orbit", Up), NewCommand("orbit", Down)));

        Assert.Equal("A command named 'Orbit' already exists in 'Blender'.", ordinary.Message);
    }

    [Fact]
    public void ARenameIsRefusedOnlyWithinTheParent_AndSoIsAMoveIntoAHoldRemapWithTheName()
    {
        var space = NewSpace();
        var s = HoldRemap.For(KeyCode.S);
        var store = new MappingStore(Document(NewGlobal(), BlenderWith(
            [space, s],
            Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle),
            Remap(space, "Pan", HoldInput.Of(MouseButton.Right), Middle),
            Remap(s, "Scale", HoldInput.Of(MouseButton.Left), Middle),
            NewCommand("Close", Up))));
        var blender = store.Current.Groups[1];
        Command Named(string name) => store.Current.Groups[1].Commands.Single(command => command.Name == name);

        var underS = store.UpdateCommand(blender.Id, Named("Scale") with { Name = "Orbit" });
        var ordinary = store.UpdateCommand(blender.Id, Named("Close") with { Name = "Orbit" });
        var refused = Assert.Throws<MappingValidationException>(() => store.UpdateCommand(blender.Id, Named("Pan") with { Name = "orbit" }));

        Assert.Equal(("Orbit", (HoldRemapId?)s.Id), (underS.Name, underS.HoldRemapId));
        Assert.Equal(("Orbit", (HoldRemapId?)null), (ordinary.Name, ordinary.HoldRemapId));
        Assert.Equal("A command named 'Orbit' already exists under 'Space' in 'Blender'.", refused.Message);
        var move = Assert.Throws<MappingValidationException>(() => store.UpdateCommand(blender.Id, underS with { HoldRemapId = space.Id, Trigger = Trigger.ForInput(HoldInput.Of(MouseButton.Middle)) }));
        Assert.Equal("A command named 'Orbit' already exists under 'Space' in 'Blender'.", move.Message);
    }

    [Fact]
    public void SiblingNamesAreTheNamesOfTheCommandsWithTheSameParent()
    {
        var space = NewSpace();
        var s = HoldRemap.For(KeyCode.S);
        var orbit = Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle);
        var commands = new[] { orbit, Remap(space, "Pan", HoldInput.Of(MouseButton.Right), Middle), Remap(s, "Scale", HoldInput.Of(MouseButton.Left), Middle), NewCommand("Close", Up) };

        Assert.Equal(["Orbit", "Pan"], CommandNames.SiblingNames(commands, space.Id));
        Assert.Equal(["Scale"], CommandNames.SiblingNames(commands, s.Id));
        Assert.Equal(["Close"], CommandNames.SiblingNames(commands, null));
        Assert.Empty(CommandNames.SiblingNames(commands, HoldRemapId.New()));
        Assert.True(CommandNames.AreSiblings(orbit, commands[1]));
        Assert.False(CommandNames.AreSiblings(orbit, commands[2]));
        Assert.False(CommandNames.AreSiblings(orbit, commands[3]));
    }

    [Fact]
    public void ACommandIsLabelledGroupHoldRemapCommand_AnOrdinaryOneGroupCommand()
    {
        var space = NewSpace();
        var blender = MappingRules.ValidDocument(Document(NewGlobal(), BlenderWith([space], Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle), NewCommand("Close", Up)))).Groups[1];
        var orbit = blender.Commands.Single(command => command.Name == "Orbit");
        var close = blender.Commands.Single(command => command.Name == "Close");

        Assert.Equal("Blender › Space › Orbit", CommandNames.Label(blender, orbit));
        Assert.Equal("Blender › Close", CommandNames.Label(blender, close));
        Assert.Equal("Global › Minimize", CommandNames.Label(AppGroup.EmptyGlobal, NewCommand("Minimize", Up)));
        Assert.Equal("under 'Space' in 'Blender'", CommandNames.Where(blender, orbit));
        Assert.Equal("in 'Blender'", CommandNames.Where(blender, close));
    }

    private static AppGroup BlenderWith(HoldRemap[] holdRemaps, params Command[] commands) => NewGroup("Blender", null, commands) with { HoldRemaps = holdRemaps };

    private static MappingValidationException Refused(AppGroup group)
        => Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), group)));
}
