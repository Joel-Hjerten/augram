using Augram.Platform.MacOS.WindowSystem;
using Xunit;

namespace Augram.Platform.MacOS.Tests.WindowSystem;

/// <summary>Hit-testing the window server's front-to-back list, and the desktop and full-screen flags.</summary>
public sealed class MacWindowPickTests
{
    private const int Own = 100;
    private static readonly MacRect Display = new(0, 0, 1512, 982);

    [Fact]
    public void At_ReturnsFrontmostWindowContainingPoint()
    {
        var front = Window(1, pid: 1, new MacRect(100, 100, 400, 300));
        var back = Window(2, pid: 2, new MacRect(0, 0, 1000, 800));

        Assert.Same(front, MacWindowPick.At([front, back], 200, 200, Own));
        Assert.Same(back, MacWindowPick.At([front, back], 50, 50, Own));
    }

    [Fact]
    public void At_SkipsOwnOverlay_SoTheTrailIsNeverTheTarget()
    {
        var overlay = Window(1, pid: Own, Display, layer: 25);
        var app = Window(2, pid: 1, new MacRect(0, 0, 800, 600));

        Assert.Same(app, MacWindowPick.At([overlay, app], 10, 10, Own));
    }

    [Fact]
    public void At_OwnNormalWindowIsATarget_LikeAnyOtherApp()
    {
        var settings = Window(1, pid: Own, new MacRect(100, 100, 900, 600));
        var app = Window(2, pid: 1, Display);

        Assert.Same(settings, MacWindowPick.At([settings, app], 200, 200, Own));
    }

    [Fact]
    public void At_SkipsTransparentAndEmptyWindows()
    {
        var invisible = Window(1, pid: 1, Display, alpha: 0);
        var empty = Window(2, pid: 2, new MacRect(10, 10, 0, 0));
        var app = Window(3, pid: 3, Display);

        Assert.Same(app, MacWindowPick.At([invisible, empty, app], 10, 10, Own));
    }

    [Fact]
    public void At_HigherLayerWins_SoAGestureOnTheDockNeverReachesTheWindowBehindIt()
    {
        var dock = Window(1, pid: 1, new MacRect(400, 900, 700, 82), layer: 20);
        var app = Window(2, pid: 2, Display);

        Assert.Same(dock, MacWindowPick.At([dock, app], 500, 950, Own));
    }

    [Fact]
    public void At_RightAndBottomEdgesAreExclusive()
    {
        var app = Window(1, pid: 1, new MacRect(0, 0, 100, 100));

        Assert.Null(MacWindowPick.At([app], 100, 50, Own));
        Assert.Null(MacWindowPick.At([app], 50, 100, Own));
        Assert.Same(app, MacWindowPick.At([app], 99.5, 99.5, Own));
    }

    [Fact]
    public void At_NothingThere_Null() => Assert.Null(MacWindowPick.At([], 10, 10, Own));

    [Fact]
    public void IsDesktop_BelowNormalLayer()
    {
        Assert.True(MacWindowPick.IsDesktop(Window(1, pid: 1, Display, layer: -2147483603)));
        Assert.False(MacWindowPick.IsDesktop(Window(2, pid: 1, Display)));
        Assert.False(MacWindowPick.IsDesktop(Window(3, pid: 1, Display, layer: 20)));
    }

    [Fact]
    public void IsFullScreen_NormalWindowCoveringADisplay()
    {
        var external = new MacRect(1512, -200, 2560, 1440);

        Assert.True(MacWindowPick.IsFullScreen(Window(1, pid: 1, Display), [Display, external]));
        Assert.True(MacWindowPick.IsFullScreen(Window(2, pid: 1, external), [Display, external]));
        Assert.False(MacWindowPick.IsFullScreen(Window(3, pid: 1, new MacRect(0, 33, 1512, 949)), [Display, external]));
        Assert.False(MacWindowPick.IsFullScreen(Window(4, pid: 1, Display, layer: -2147483624), [Display]));
    }

    private static MacWindowInfo Window(uint id, int pid, MacRect bounds, int layer = 0, double alpha = 1)
        => new(id, pid, "App" + pid, null, layer, alpha, bounds);
}
