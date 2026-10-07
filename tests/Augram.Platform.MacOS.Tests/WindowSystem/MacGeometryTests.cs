using Augram.Platform.MacOS.WindowSystem;
using Xunit;

namespace Augram.Platform.MacOS.Tests.WindowSystem;

/// <summary>The Cocoa ↔ top-left flip, and the maximize rules (D6): what counts as filled, which screen a window is on.</summary>
public sealed class MacGeometryTests
{
    // A 1512×982 main display with a 2560×1440 display to its right whose top is 200 pt higher.
    private const double MainHeight = 982;
    private static readonly MacRect Main = new(0, 0, 1512, 982);
    private static readonly MacRect External = new(1512, -200, 2560, 1440);

    [Fact]
    public void FromCocoa_MainScreenMapsOntoItself()
        => Assert.Equal(Main, MacRect.FromCocoa(0, 0, 1512, 982, MainHeight));

    [Fact]
    public void FromCocoa_VisibleFrameBelowTheMenuBar()
    {
        // NSScreen.visibleFrame of the main screen: the Dock takes 60 pt at the bottom, the menu bar 33 pt at the top.
        var visible = MacRect.FromCocoa(0, 60, 1512, 889, MainHeight);

        Assert.Equal(new MacRect(0, 33, 1512, 889), visible);
    }

    [Fact]
    public void FromCocoa_ScreenAboveTheMainScreensTopHasNegativeY()
        => Assert.Equal(External, MacRect.FromCocoa(1512, -258, 2560, 1440, MainHeight));

    [Fact]
    public void CocoaY_IsTheInverseOfFromCocoa()
    {
        var rect = new MacRect(1600, -150, 800, 600);

        Assert.Equal(rect, MacRect.FromCocoa(rect.X, rect.CocoaY(MainHeight), rect.Width, rect.Height, MainHeight));
    }

    [Fact]
    public void Fills_WithinToleranceOnEveryEdge()
    {
        var visible = new MacRect(0, 33, 1512, 889);

        Assert.True(MacMaximize.Fills(visible, visible));
        Assert.True(MacMaximize.Fills(new MacRect(0, 33, 1505, 880), visible));
        Assert.False(MacMaximize.Fills(new MacRect(0, 33, 1400, 889), visible));
        Assert.False(MacMaximize.Fills(new MacRect(100, 133, 800, 600), visible));
    }

    [Fact]
    public void ScreenFor_TheScreenContainingTheCentre()
    {
        Assert.Equal(0, MacMaximize.ScreenFor(new MacRect(100, 100, 400, 300), [Main, External]));
        Assert.Equal(1, MacMaximize.ScreenFor(new MacRect(2000, 0, 800, 600), [Main, External]));
    }

    [Fact]
    public void ScreenFor_CentreOffEveryScreen_TheOneOverlappedMost()
    {
        // Centre at (1450, 1100): below the main screen, left of the external one; overlaps the main by 212×82, the external by 88×340.
        Assert.Equal(1, MacMaximize.ScreenFor(new MacRect(1300, 900, 300, 400), [Main, External]));
    }

    [Fact]
    public void ScreenFor_NoOverlapAtAll_TheFirst()
        => Assert.Equal(0, MacMaximize.ScreenFor(new MacRect(-5000, -5000, 10, 10), [Main, External]));
}
