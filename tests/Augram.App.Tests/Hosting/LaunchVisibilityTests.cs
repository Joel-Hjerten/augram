using Augram.App.Hosting;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>A launch opens its window unless it carries --hidden (the Windows Run entry) or macOS launched it as a login item.</summary>
public sealed class LaunchVisibilityTests
{
    [Fact]
    public void APlainLaunch_ShowsTheWindow()
    {
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.Decide([], loginItemLaunch: false));
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.Decide(null, loginItemLaunch: false));
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.Decide(["--no-engine", "--config-folder", @"D:\x"], loginItemLaunch: false));
    }

    [Theory]
    [InlineData("--hidden")]
    [InlineData("--HIDDEN")]
    public void HiddenArgument_StartsInTheTray(string argument)
    {
        var launch = LaunchVisibility.Decide(["--no-engine", argument], loginItemLaunch: false);

        Assert.True(launch.Hidden);
        Assert.Equal(LaunchVisibility.HiddenArgumentReason, launch.Reason);
    }

    [Fact]
    public void ALoginItemLaunch_StartsInTheTray()
    {
        var launch = LaunchVisibility.Decide([], loginItemLaunch: true);

        Assert.True(launch.Hidden);
        Assert.Equal(LaunchVisibility.LoginItemReason, launch.Reason);
    }

    [Fact]
    public void TheArgumentNamesTheReason_WhenBothSayHidden()
    {
        Assert.Equal(LaunchVisibility.HiddenArgumentReason, LaunchVisibility.Decide(["--hidden"], loginItemLaunch: true).Reason);
    }

    [Fact]
    public void AnArgumentThatOnlyContainsTheWord_DoesNotHide()
    {
        Assert.False(LaunchVisibility.Decide(["--hidden-not", "hidden", "-hidden"], loginItemLaunch: false).Hidden);
    }

    [Fact]
    public void AtStart_WindowsKnowsAtOnce_MacOSWaitsForTheLaunchEventUnlessHidden()
    {
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.AtStart([], waitsForLaunchEvent: false));
        Assert.True(LaunchVisibility.AtStart(["--hidden"], waitsForLaunchEvent: false)!.Hidden);
        Assert.Null(LaunchVisibility.AtStart([], waitsForLaunchEvent: true));
        Assert.True(LaunchVisibility.AtStart(["--hidden"], waitsForLaunchEvent: true)!.Hidden);
    }

    [Fact]
    public void TheLogSaysHiddenAndWhy()
    {
        Assert.Equal([new LogProperty("hidden", true), new LogProperty("reason", "--hidden")], LaunchVisibility.Decide(["--hidden"], false).LogProperties);
        Assert.Equal([new LogProperty("hidden", true), new LogProperty("reason", "login item")], LaunchVisibility.Decide([], true).LogProperties);
        Assert.Equal([new LogProperty("hidden", false)], LaunchVisibility.Shown.LogProperties);
    }
}
