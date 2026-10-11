using Augram.App.Hosting;
using Augram.Core.Capture;
using Augram.Core.Config;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class SettingsFollowerTests
{
    [Fact]
    public void PushesOnlyWhenTheFollowedPartChanged_WithTheLastAndTheNewValue()
    {
        var settings = new SettingsStore(Settings.Default);
        var pushed = new List<(MouseButton Last, MouseButton Next)>();
        using var follower = new SettingsFollower<MouseButton>(settings, current => current.General.StrokeButton, (last, next) => pushed.Add((last, next)));

        settings.SetGeneral(settings.Current.General with { Enabled = !settings.Current.General.Enabled });
        Assert.Empty(pushed);

        settings.SetGeneral(settings.Current.General with { StrokeButton = MouseButton.Middle });
        Assert.Equal([(MouseButton.Right, MouseButton.Middle)], pushed);
        Assert.Equal(MouseButton.Middle, follower.Current);

        follower.Dispose();
        settings.SetGeneral(settings.Current.General with { StrokeButton = MouseButton.Right });
        Assert.Single(pushed);
    }
}
