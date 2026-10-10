using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// A Global command's "Not in" (Joel, 2026-10-10, plan 0004): over a window of an app group it names, the command is as if it
/// did not exist, so it fires nothing and holds no button back there; everywhere else it is unchanged.
/// </summary>
public sealed class NotInTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right));

    [Fact]
    public void AGlobalCommandNotInAnApp_FiresNothingThere_AndStillFiresElsewhere()
    {
        var spine = NewGroup("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(zoom), spine));

        var inSpine = CommandResolver.Resolve(mapping, Window("spine.exe"), RightWheelDown, HostPlatform.Windows);
        Assert.Equal(ResolutionOutcome.None, inSpine.Outcome);
        Assert.Equal("'Zoom In' is not used in 'Spine'", inSpine.Reason);

        Assert.Equal(ResolutionOutcome.Matched, CommandResolver.Resolve(mapping, Window("chrome.exe"), RightWheelDown, HostPlatform.Windows).Outcome);
        Assert.Equal(ResolutionOutcome.Matched, CommandResolver.Resolve(mapping, null, RightWheelDown, HostPlatform.Windows).Outcome);
    }

    [Fact]
    public void AGlobalCommandNotInAnApp_HoldsNoButtonBackThere()
    {
        var spine = NewGroup("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(zoom), spine));

        Assert.False(AnchorPlanner.For(mapping, Window("spine.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.True(AnchorPlanner.For(mapping, Window("chrome.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.True(AnchorPlanner.For(mapping, null, HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
    }

    [Fact]
    public void AnAppsOwnCommandOnTheSameTrigger_StillAppliesThere()
    {
        var spine = NewGroup("Spine", null, NewCommand("Spine zoom", RightWheelDown, NewStep("spine")));
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(zoom), spine));

        var resolution = CommandResolver.Resolve(mapping, Window("spine.exe"), RightWheelDown, HostPlatform.Windows);
        Assert.Equal("Spine zoom", resolution.Command?.Name);
        Assert.True(AnchorPlanner.For(mapping, Window("spine.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
    }

    [Fact]
    public void TheRules_KeepOnlyExistingAppGroups_OnceEach_SortedById_AndOnlyOnGlobalCommands()
    {
        var spine = NewGroup("Spine");
        var eyeris = NewGroup("Eyeris");
        var gone = GroupId.New();
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [eyeris.Id, gone, spine.Id, eyeris.Id, GroupId.Global] };
        var appCommand = NewCommand("Local", RightWheelDown, NewStep("local")) with { NotIn = [spine.Id] };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(zoom), spine, eyeris with { Commands = [appCommand] }));

        var stored = mapping.Global.Commands.Single();
        Assert.Equal(new[] { spine.Id, eyeris.Id }.OrderBy(id => id.Value), stored.NotIn);
        Assert.Empty(mapping.Groups.Single(group => group.Name == "Eyeris").Commands.Single().NotIn);
    }

    [Fact]
    public void RemovingAnAppGroup_DropsItFromEveryNotIn()
    {
        var spine = NewGroup("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var store = new MappingStore(MappingRules.ValidDocument(Document(NewGlobal(zoom), spine)));

        store.RemoveGroup(spine.Id);

        Assert.Empty(store.Current.Global.Commands.Single().NotIn);
    }
}
