using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Input;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>The notifications <c>MacSystemEvents</c> observes and what each means; the observers themselves need AppKit's run loop (checked by hand on the Mac).</summary>
public sealed class MacWorkspaceEventsTests
{
    [Theory]
    [InlineData("NSWorkspaceDidActivateApplicationNotification", SystemEventKind.ForegroundChanged)]
    [InlineData("NSWorkspaceWillSleepNotification", SystemEventKind.Suspending)]
    [InlineData("NSWorkspaceDidWakeNotification", SystemEventKind.Resumed)]
    [InlineData("NSWorkspaceSessionDidResignActiveNotification", SystemEventKind.SessionLocked)]
    [InlineData("NSWorkspaceSessionDidBecomeActiveNotification", SystemEventKind.SessionUnlocked)]
    [InlineData("com.apple.screenIsLocked", SystemEventKind.SessionLocked)]
    [InlineData("com.apple.screenIsUnlocked", SystemEventKind.SessionUnlocked)]
    [InlineData("NSWorkspaceWillPowerOffNotification", SystemEventKind.SessionEnding)]
    [InlineData("NSApplicationDidChangeScreenParametersNotification", SystemEventKind.DisplayChanged)]
    public void EachObservedNotificationMaps(string name, SystemEventKind expected) => Assert.Equal(expected, MacWorkspaceEvents.Map(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NSWorkspaceDidLaunchApplicationNotification")]
    [InlineData("NSWorkspaceDidDeactivateApplicationNotification")]
    public void AnythingElseMapsToNothing(string? name) => Assert.Null(MacWorkspaceEvents.Map(name));

    [Fact]
    public void EveryObservedNameMaps_AndEachIsObservedInOneCenterOnly()
    {
        var all = MacWorkspaceEvents.Workspace.Concat(MacWorkspaceEvents.Distributed).Concat(MacWorkspaceEvents.Application).ToList();

        Assert.All(all, name => Assert.NotNull(MacWorkspaceEvents.Map(name)));
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(MacWorkspaceEvents.DidActivateApplication, MacWorkspaceEvents.Workspace);
        Assert.Equal([MacWorkspaceEvents.ScreenIsLocked, MacWorkspaceEvents.ScreenIsUnlocked], MacWorkspaceEvents.Distributed);
    }
}
