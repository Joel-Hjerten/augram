using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>Who wins when the setting and the OS disagree: the setting, except over an "off" made outside Augram, which startup follows.</summary>
public sealed class StartupPolicyTests
{
    [Theory]
    [InlineData(StartupStatus.Registered, StartupAction.None)]
    [InlineData(StartupStatus.NotRegistered, StartupAction.Register)]
    [InlineData(StartupStatus.Outdated, StartupAction.Register)]
    [InlineData(StartupStatus.DisabledByUser, StartupAction.FollowTheSystem)]
    [InlineData(StartupStatus.NeedsApproval, StartupAction.FollowTheSystem)]
    public void AtStartup_On(StartupStatus status, StartupAction expected) => Assert.Equal(expected, StartupPolicy.AtStartup(true, status));

    [Theory]
    [InlineData(StartupStatus.Registered, StartupAction.None)]
    [InlineData(StartupStatus.NotRegistered, StartupAction.Register)]
    [InlineData(StartupStatus.Outdated, StartupAction.Register)]
    [InlineData(StartupStatus.DisabledByUser, StartupAction.Register)]
    [InlineData(StartupStatus.NeedsApproval, StartupAction.Register)]
    public void TurnedOnInAugram_MeansOn(StartupStatus status, StartupAction expected) => Assert.Equal(expected, StartupPolicy.OnChange(true, status));

    [Theory]
    [InlineData(StartupStatus.NotRegistered, StartupAction.None)]
    [InlineData(StartupStatus.Registered, StartupAction.Unregister)]
    [InlineData(StartupStatus.Outdated, StartupAction.Unregister)]
    [InlineData(StartupStatus.DisabledByUser, StartupAction.Unregister)]
    [InlineData(StartupStatus.NeedsApproval, StartupAction.Unregister)]
    public void Off_RemovesWhateverIsThere_AtStartupAndOnChange(StartupStatus status, StartupAction expected)
    {
        Assert.Equal(expected, StartupPolicy.AtStartup(false, status));
        Assert.Equal(expected, StartupPolicy.OnChange(false, status));
    }

    [Fact]
    public void TheFollowedNote_NamesWhereItWasTurnedOff()
    {
        Assert.Equal("Turned off in Task Manager › Startup apps.", StartupPolicy.FollowedNote(StartupStatus.DisabledByUser, HostPlatform.Windows));
        Assert.Equal("Removed in System Settings › General › Login Items.", StartupPolicy.FollowedNote(StartupStatus.DisabledByUser, HostPlatform.MacOS));
        Assert.Equal("Not allowed in System Settings › General › Login Items.", StartupPolicy.FollowedNote(StartupStatus.NeedsApproval, HostPlatform.MacOS));
        Assert.Empty(StartupPolicy.FollowedNote(StartupStatus.NotRegistered, HostPlatform.Windows));
        Assert.Empty(StartupPolicy.FollowedNote(StartupStatus.Registered, HostPlatform.MacOS));
    }

    [Fact]
    public void TheStatusNote_IsOnlyForAPendingApproval()
    {
        Assert.Equal("Waiting for approval in System Settings › General › Login Items.", StartupPolicy.StatusNote(true, StartupStatus.NeedsApproval));
        Assert.Empty(StartupPolicy.StatusNote(false, StartupStatus.NeedsApproval));
        Assert.Empty(StartupPolicy.StatusNote(true, StartupStatus.Registered));
        Assert.Empty(StartupPolicy.StatusNote(true, StartupStatus.DisabledByUser));
    }
}
