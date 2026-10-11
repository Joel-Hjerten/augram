using Augram.App.Hosting.Launch;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>A launch opens its window unless it carries --hidden (the Windows Run entry) or macOS launched it as a login item.</summary>
public sealed class LaunchVisibilityTests
{
    [Fact]
    public void APlainLaunch_ShowsTheWindow()
    {
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.Decide(hiddenArgument: false, loginItemLaunch: false));
        Assert.False(LaunchVisibility.HasHiddenArgument([]));
        Assert.False(LaunchVisibility.HasHiddenArgument(null));
        Assert.False(LaunchVisibility.HasHiddenArgument(["--no-engine", "--config-folder", @"D:\x"]));
    }

    [Theory]
    [InlineData("--hidden")]
    [InlineData("--HIDDEN")]
    public void HiddenArgument_StartsInTheTray(string argument)
    {
        Assert.True(LaunchVisibility.HasHiddenArgument(["--no-engine", argument]));
        var launch = LaunchVisibility.Decide(hiddenArgument: true, loginItemLaunch: false);

        Assert.True(launch.Hidden);
        Assert.Equal(LaunchVisibility.HiddenArgumentReason, launch.Reason);
    }

    [Fact]
    public void ALoginItemLaunch_StartsInTheTray()
    {
        var launch = LaunchVisibility.Decide(hiddenArgument: false, loginItemLaunch: true);

        Assert.True(launch.Hidden);
        Assert.Equal(LaunchVisibility.LoginItemReason, launch.Reason);
    }

    [Fact]
    public void TheArgumentNamesTheReason_WhenBothSayHidden()
    {
        Assert.Equal(LaunchVisibility.HiddenArgumentReason, LaunchVisibility.Decide(hiddenArgument: true, loginItemLaunch: true).Reason);
    }

    [Fact]
    public void AnArgumentThatOnlyContainsTheWord_DoesNotHide()
    {
        Assert.False(LaunchVisibility.HasHiddenArgument(["--hidden-not", "hidden", "-hidden"]));
    }

    [Fact]
    public void AtStart_WindowsKnowsAtOnce_MacOSWaitsForTheLaunchEventUnlessHidden()
    {
        Assert.Equal(LaunchVisibility.Shown, LaunchVisibility.AtStart(hiddenArgument: false, waitsForLaunchEvent: false));
        Assert.True(LaunchVisibility.AtStart(hiddenArgument: true, waitsForLaunchEvent: false)!.Hidden);
        Assert.Null(LaunchVisibility.AtStart(hiddenArgument: false, waitsForLaunchEvent: true));
        Assert.True(LaunchVisibility.AtStart(hiddenArgument: true, waitsForLaunchEvent: true)!.Hidden);
    }

    [Fact]
    public void TheLogSaysHiddenAndWhy()
    {
        Assert.Equal([new LogProperty("hidden", true), new LogProperty("reason", "--hidden")], LaunchVisibility.Decide(true, false).LogProperties);
        Assert.Equal([new LogProperty("hidden", true), new LogProperty("reason", "login item")], LaunchVisibility.Decide(false, true).LogProperties);
        Assert.Equal([new LogProperty("hidden", false)], LaunchVisibility.Shown.LogProperties);
    }
}
