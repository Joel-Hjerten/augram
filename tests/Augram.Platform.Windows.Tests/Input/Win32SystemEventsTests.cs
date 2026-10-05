using Augram.Core.Abstractions;
using Augram.Platform.Windows.Input;
using Microsoft.Win32;
using Xunit;

namespace Augram.Platform.Windows.Tests.Input;

/// <summary>The OS-to-Core mapping; the subscription itself needs a session switch to exercise.</summary>
public sealed class Win32SystemEventsTests
{
    [Theory]
    [InlineData(SessionSwitchReason.SessionLock, SystemEventKind.SessionLocked)]
    [InlineData(SessionSwitchReason.SessionUnlock, SystemEventKind.SessionUnlocked)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, SystemEventKind.SessionLocked)]
    [InlineData(SessionSwitchReason.ConsoleConnect, SystemEventKind.SessionUnlocked)]
    [InlineData(SessionSwitchReason.SessionLogoff, SystemEventKind.SessionEnding)]
    public void MapsSessionSwitches(SessionSwitchReason reason, SystemEventKind expected)
    {
        Assert.Equal(expected, Win32SystemEvents.Map(reason));
    }

    [Fact]
    public void MapsPowerModes_AndIgnoresStatusChange()
    {
        Assert.Equal(SystemEventKind.Suspending, Win32SystemEvents.Map(PowerModes.Suspend));
        Assert.Equal(SystemEventKind.Resumed, Win32SystemEvents.Map(PowerModes.Resume));
        Assert.Null(Win32SystemEvents.Map(PowerModes.StatusChange));
        Assert.Null(Win32SystemEvents.Map(SessionSwitchReason.SessionRemoteControl));
    }

    [Fact]
    public void SubscribesAndUnsubscribesWithoutError()
    {
        using var events = new Win32SystemEvents();
        events.Occurred += (_, _) => { };
    }
}
