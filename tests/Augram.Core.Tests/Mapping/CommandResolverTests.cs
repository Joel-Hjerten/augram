using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

public sealed class CommandResolverTests
{
    private static readonly Trigger WheelUp = Trigger.ForWheel(WheelDirection.Up);
    private static readonly Trigger WheelDown = Trigger.ForWheel(WheelDirection.Down);
    private static readonly Trigger UpGesture = Trigger.ForGesture(Up);
    private static readonly Trigger DownGesture = Trigger.ForGesture(Down);

    /// <summary>The reference config in miniature: Global, Chrome override, Steam override to nothing, FF7 suppressing globals, an inactive group, one ignored game.</summary>
    private static readonly MappingDocument Mapping = MappingRules.ValidDocument(new MappingDocument(
        [
            NewGlobal(NewCommand("Close", Up, NewStep("close")), NewCommand("Volume up", WheelUp, NewStep("vol+"))),
            NewGroup("Chrome", commands: [NewCommand("Close tab", Up, NewStep("ctrl+w")), NewCommand("Off", Down, NewStep("x")) with { IsActive = false }]),
            NewGroup("Steam", commands: [NewCommand("Nothing", Up)]),
            NewGroup("FF7", commands: []) with { SuppressGlobals = true },
            NewGroup("Dormant", commands: [NewCommand("Never", Up, NewStep("never"))]) with { IsActive = false },
            NewGroup("Game"),
        ],
        [
            new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true),
            new IgnoredApp(GroupId.New(), "Old", IsActive: false, ByProcess("notepad.exe"), DisableEntirely: false),
        ]));

    [Fact]
    public void AGlobalCommandFiresWhereNoAppGroupMatches()
    {
        var resolution = CommandResolver.Resolve(Mapping, Window("notepad.exe"), UpGesture);

        Assert.Equal(ResolutionOutcome.Matched, resolution.Outcome);
        Assert.True(resolution.Group!.IsGlobal);
        Assert.Equal("Close", resolution.Command!.Name);
        Assert.Equal("global", resolution.Reason);
        Assert.True(resolution.Fires);
    }

    [Fact]
    public void NoWindowResolvesAgainstGlobalAlone()
    {
        var resolution = CommandResolver.Resolve(Mapping, target: null, WheelUp);

        Assert.Equal("Volume up", resolution.Command!.Name);
        Assert.Equal("global", resolution.Reason);
    }

    [Fact]
    public void AnAppOverrideShadowsTheGlobalCommand()
    {
        var resolution = CommandResolver.Resolve(Mapping, Window("chrome.exe"), UpGesture);

        Assert.Equal("Chrome", resolution.Group!.Name);
        Assert.Equal("Close tab", resolution.Command!.Name);
        Assert.Equal("app override in 'Chrome'", resolution.Reason);
    }

    [Fact]
    public void AnOverrideToNothingMatchesButDoesNotFire()
    {
        var resolution = CommandResolver.Resolve(Mapping, Window("steam.exe"), UpGesture);

        Assert.Equal(ResolutionOutcome.Matched, resolution.Outcome);
        Assert.True(resolution.Command!.IsOverrideToNothing);
        Assert.False(resolution.Fires);
        Assert.Equal("override to nothing in 'Steam'", resolution.Reason);
    }

    [Fact]
    public void ATriggerTheAppDoesNotBindFallsThroughToGlobal()
    {
        var resolution = CommandResolver.Resolve(Mapping, Window("chrome.exe"), WheelUp);

        Assert.Equal("Volume up", resolution.Command!.Name);
        Assert.Equal("global", resolution.Reason);
    }

    [Fact]
    public void SuppressGlobalsBlocksTheFallThrough()
    {
        var resolution = CommandResolver.Resolve(Mapping, Window("ff7.exe"), UpGesture);

        Assert.Equal(ResolutionOutcome.None, resolution.Outcome);
        Assert.Null(resolution.Command);
        Assert.Equal("globals suppressed by 'FF7'", resolution.Reason);
    }

    [Fact]
    public void AnIgnoredAppWinsOverAMatchingGroupAndAnInactiveOneIsInvisible()
    {
        var ignored = CommandResolver.Resolve(Mapping, Window("game.exe"), UpGesture);

        Assert.Equal(ResolutionOutcome.Ignored, ignored.Outcome);
        Assert.Equal("ignored app 'Game'", ignored.Reason);
        Assert.True(ignored.IgnoredBy!.DisableEntirely);
        Assert.Null(ignored.Group);
        Assert.False(ignored.Fires);
        Assert.NotNull(CommandResolver.FindIgnored(Mapping, Window("game.exe")));

        Assert.Equal("global", CommandResolver.Resolve(Mapping, Window("notepad.exe"), UpGesture).Reason);
        Assert.Null(CommandResolver.FindIgnored(Mapping, null));
    }

    [Fact]
    public void InactiveGroupsAndCommandsAreInvisible()
    {
        Assert.Equal("global", CommandResolver.Resolve(Mapping, Window("dormant.exe"), UpGesture).Reason);
        Assert.Null(CommandResolver.FindGroup(Mapping, Window("dormant.exe")));

        var resolution = CommandResolver.Resolve(Mapping, Window("chrome.exe"), DownGesture);
        Assert.Equal(ResolutionOutcome.None, resolution.Outcome);
        Assert.Equal("no command for this gesture", resolution.Reason);
    }

    [Fact]
    public void NoTriggerAndUnboundWheelTicksResolveToNoneWithAReason()
    {
        Assert.Equal("no trigger", CommandResolver.Resolve(Mapping, Window(), Trigger.None).Reason);
        Assert.Equal("no command for wheel down", CommandResolver.Resolve(Mapping, Window(), WheelDown).Reason);
    }

    [Fact]
    public void AnInactiveGlobalGroupFiresNothing()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Close", Up, NewStep("x"))) with { IsActive = false }));

        var resolution = CommandResolver.Resolve(mapping, Window(), UpGesture);

        Assert.Equal(ResolutionOutcome.None, resolution.Outcome);
        Assert.Equal("the Global group is inactive", resolution.Reason);
    }

    [Fact]
    public void TheFirstMatchingGroupInDocumentOrderWins()
    {
        var mapping = MappingRules.ValidDocument(Document(
            NewGlobal(),
            NewGroup("Chrome", commands: [NewCommand("Specific", Up, NewStep("s"))]),
            NewGroup("Browsers", ByProcess("chrome.exe", "msedge.exe"), NewCommand("Shared", Up, NewStep("b")))));

        var resolution = CommandResolver.Resolve(mapping, Window("chrome.exe"), UpGesture);

        Assert.Equal("Browsers", resolution.Group!.Name);
        Assert.Equal("Browsers", CommandResolver.FindGroup(mapping, Window("chrome.exe"))!.Name);
    }
}
