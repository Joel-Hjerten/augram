using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>A21: rect equals monitor rect, desktop excluded.</summary>
public sealed class FullScreenRuleTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 3840, 2160);

    [Fact]
    public void WindowCoveringMonitor_IsFullScreen()
    {
        Assert.True(FullScreenRule.IsFullScreen(Monitor, Monitor, "UnityWndClass"));
    }

    [Fact]
    public void MaximizedWindowOverlappingEdges_IsNotFullScreen()
    {
        var maximized = new ScreenRect(-8, -8, 3848, 2112);

        Assert.False(FullScreenRule.IsFullScreen(maximized, Monitor, "Chrome_WidgetWin_1"));
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    public void DesktopCoveringMonitor_IsNotFullScreen(string rootClass)
    {
        Assert.False(FullScreenRule.IsFullScreen(Monitor, Monitor, rootClass));
    }

    [Fact]
    public void SecondMonitorOffset_ComparesExactEdges()
    {
        var second = new ScreenRect(3840, 0, 5760, 1080);

        Assert.True(FullScreenRule.IsFullScreen(second, second, "SDL_app"));
        Assert.False(FullScreenRule.IsFullScreen(new ScreenRect(3840, 0, 5760, 1079), second, "SDL_app"));
    }
}
