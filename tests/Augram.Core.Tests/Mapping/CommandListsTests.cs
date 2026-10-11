using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>The lookups and lists every caller shares (plan 0007): a group's commands by parent and platform, a command by id, a hold remap's binding by id.</summary>
public sealed class CommandListsTests
{
    [Fact]
    public void AGroupListsItsOrdinaryCommands_ItsHoldRemapsCommands_AndTheActiveOnesWithTheirTrigger()
    {
        var space = NewSpace();
        var close = NewCommand("Close", Up, NewStep("close"));
        var windowsOnly = NewCommand("Snap", Down, NewStep("snap")) with { UseOn = PlatformSet.Windows };
        var off = NewCommand("Off", Trigger.ForWheel(WheelDirection.Up), NewStep("off")) with { IsActive = false };
        var orbit = Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), Middle);
        var group = NewGroup("Blender", ByProcess("blender.exe"), close, windowsOnly, off, orbit) with { HoldRemaps = [space] };

        Assert.Equal(["Close", "Off"], group.OrdinaryCommandsOn(HostPlatform.MacOS).Select(command => command.Name));
        Assert.Equal(["Close", "Snap", "Off"], group.OrdinaryCommandsOn(null).Select(command => command.Name));
        Assert.Equal(["Orbit"], group.HoldRemapCommandsOn(space, HostPlatform.MacOS).Select(command => command.Name));
        Assert.Equal(
            [("Close", Trigger.ForGesture(Up)), ("Snap", Trigger.ForGesture(Down))],
            group.ActiveCommandsOn(HostPlatform.Windows).Select(pair => (pair.Command.Name, pair.Trigger)));
    }

    [Fact]
    public void ACommandIsFoundByIdInAnyGroup_AndABindingInItsHoldRemap()
    {
        var mapping = Document();
        var blender = mapping.Groups[1];
        var orbit = blender.Commands.Single(command => command.Name == "Orbit");

        var space = HoldRemapPlan.ForGroup(blender, HostPlatform.Windows).Entries.Single();

        Assert.Equal((blender, orbit), mapping.FindCommand(orbit.Id));
        Assert.Null(mapping.FindCommand(CommandId.New()));
        Assert.Equal("Orbit", space.FindBinding(orbit.Id)?.Name);
        Assert.Null(space.FindBinding(CommandId.New()));
    }
}
