using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// A command's "Not in" (Joel, 2026-10-10, plan 0004): it names entries of Ignored › Per command, apps that do nothing on their
/// own. Over a window one of them claims, the command is as if it did not exist, so it fires nothing and holds no button back
/// there; an app group's command falls through to Global's. An Ignored › Global entry still stops everything.
/// </summary>
public sealed class NotInTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right));

    private static IgnoredApp PerCommand(string name, bool active = true)
        => new(GroupId.New(), name, active, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    private static MappingDocument Document(AppGroup[] groups, params IgnoredApp[] ignored)
        => MappingRules.ValidDocument(new MappingDocument(groups, ignored));

    [Fact]
    public void ACommandNotInAnApp_FiresNothingThere_AndStillFiresElsewhere()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var mapping = Document([NewGlobal(zoom)], spine);

        var inSpine = CommandResolver.Resolve(mapping, Window("spine.exe"), RightWheelDown, HostPlatform.Windows);
        Assert.Equal(ResolutionOutcome.None, inSpine.Outcome);
        Assert.Equal("'Zoom In' is not used in 'Spine'", inSpine.Reason);

        Assert.Equal(ResolutionOutcome.Matched, CommandResolver.Resolve(mapping, Window("chrome.exe"), RightWheelDown, HostPlatform.Windows).Outcome);
        Assert.Equal(ResolutionOutcome.Matched, CommandResolver.Resolve(mapping, null, RightWheelDown, HostPlatform.Windows).Outcome);
    }

    [Fact]
    public void APerCommandEntry_StopsNothingOnItsOwn_AndAnInactiveOneStopsNoCommand()
    {
        var spine = PerCommand("Spine");
        var off = PerCommand("Eyeris", active: false);
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [off.Id] };
        var close = NewCommand("Close", Up, NewStep("close"));
        var mapping = Document([NewGlobal(zoom, close)], spine, off);

        Assert.Null(CommandResolver.FindIgnored(mapping, Window("spine.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.Under(mapping, Window("spine.exe"), HostPlatform.Windows));
        Assert.False(IgnoreList.WatchesPointer(mapping, HostPlatform.Windows));
        Assert.Equal("Close", CommandResolver.Resolve(mapping, Window("spine.exe"), Trigger.ForGesture(Up), HostPlatform.Windows).Command?.Name);
        Assert.Equal("Zoom In", CommandResolver.Resolve(mapping, Window("eyeris.exe"), RightWheelDown, HostPlatform.Windows).Command?.Name);
    }

    [Fact]
    public void ACommandNotInAnApp_HoldsNoButtonBackThere()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var mapping = Document([NewGlobal(zoom)], spine);

        Assert.True(IgnoreList.UsesPerCommand(mapping, HostPlatform.Windows));
        Assert.False(AnchorPlanner.For(mapping, Window("spine.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.True(AnchorPlanner.For(mapping, Window("chrome.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.True(AnchorPlanner.For(mapping, null, HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
    }

    [Fact]
    public void AnAppGroupsCommandNotInAnApp_FallsThroughToGlobalThere()
    {
        var game = PerCommand("Game");
        var steam = NewGroup("Steam", ByProcess("game.exe", "other.exe"), NewCommand("Steam zoom", RightWheelDown, NewStep("steam")) with { NotIn = [game.Id] });
        var mapping = Document([NewGlobal(NewCommand("Zoom In", RightWheelDown, NewStep("zoom"))), steam], game);

        Assert.Equal("Zoom In", CommandResolver.Resolve(mapping, Window("game.exe"), RightWheelDown, HostPlatform.Windows).Command?.Name);
        Assert.Equal("Steam zoom", CommandResolver.Resolve(mapping, Window("other.exe"), RightWheelDown, HostPlatform.Windows).Command?.Name);
    }

    [Fact]
    public void AnIgnoredGlobalEntry_StillStopsEverything()
    {
        var spine = new IgnoredApp(GroupId.New(), "Spine", IsActive: true, ByProcess("spine.exe"), DisableEntirely: false);
        var mapping = Document([NewGlobal(NewCommand("Zoom In", RightWheelDown, NewStep("zoom")))], spine);

        Assert.Equal(ResolutionOutcome.Ignored, CommandResolver.Resolve(mapping, Window("spine.exe"), RightWheelDown, HostPlatform.Windows).Outcome);
    }

    [Fact]
    public void TheRules_KeepOnlyExistingPerCommandEntries_OnceEach_SortedById_AndNoneUnderAHoldRemap()
    {
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");
        var global = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, ByProcess("vmware.exe"), DisableEntirely: true);
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [eyeris.Id, GroupId.New(), spine.Id, eyeris.Id, global.Id] };
        var mapping = Document([NewGlobal(zoom)], spine, eyeris, global);

        Assert.Equal(new[] { spine.Id, eyeris.Id }.OrderBy(id => id.Value), mapping.Global.Commands.Single().NotIn);
    }

    [Fact]
    public void APerCommandEntry_NeverDisablesAugramWhileFocused()
    {
        var spine = PerCommand("Spine") with { DisableEntirely = true };
        var mapping = Document([NewGlobal()], spine);

        Assert.False(mapping.Ignored.Single().DisableEntirely);
        Assert.Null(IgnoreList.PausedBy(mapping, Window("spine.exe"), HostPlatform.Windows));
    }

    [Fact]
    public void RemovingAPerCommandEntry_DropsItFromEveryNotIn()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [spine.Id] };
        var store = new MappingStore(Document([NewGlobal(zoom)], spine));

        store.RemoveIgnored(spine.Id);

        Assert.Empty(store.Current.Global.Commands.Single().NotIn);
    }
}
