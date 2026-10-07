using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>Centre, resize-with-clamp and the two halves, in physical pixels over a work area that excludes the taskbar.</summary>
public sealed class WindowGeometryTests
{
    private static readonly ScreenRect Work = new(0, 0, 3840, 2100);
    private static readonly ScreenRect SecondWork = new(3840, 0, 5760, 1040);
    private static readonly ScreenRect Window = new(100, 100, 900, 700);

    [Fact]
    public void Center_SmallerWindow_IsCentredKeepingSize()
    {
        Assert.Equal(new ScreenRect(1520, 750, 2320, 1350), WindowGeometry.Center(Window, Work));
    }

    [Fact]
    public void Center_OnSecondMonitor_CentresInThatWorkArea()
    {
        Assert.Equal(new ScreenRect(4400, 220, 5200, 820), WindowGeometry.Center(Window, SecondWork));
    }

    [Fact]
    public void Center_WindowLargerThanWorkArea_PinsTopLeftToTheWorkArea()
    {
        var huge = new ScreenRect(-500, -500, 3500, 1700);

        Assert.Equal(new ScreenRect(0, 0, 4000, 2200), WindowGeometry.Center(huge, Work));
        Assert.Equal(new ScreenRect(3840, 0, 7840, 2200), WindowGeometry.Center(huge, SecondWork));
    }

    [Fact]
    public void Resize_KeepsTopLeft()
    {
        Assert.Equal(new ScreenRect(100, 100, 1100, 900), WindowGeometry.Resize(Window, 1000, 800, Work));
    }

    [Fact]
    public void Resize_OverflowingRight_ShiftsBackInside()
    {
        var nearRightEdge = new ScreenRect(3000, 100, 3800, 700);

        Assert.Equal(new ScreenRect(2840, 100, 3840, 900), WindowGeometry.Resize(nearRightEdge, 1000, 800, Work));
    }

    [Fact]
    public void Resize_OverflowingBottom_ShiftsBackInside()
    {
        var nearBottom = new ScreenRect(100, 1500, 900, 2100);

        Assert.Equal(new ScreenRect(100, 1300, 1100, 2100), WindowGeometry.Resize(nearBottom, 1000, 800, Work));
    }

    [Fact]
    public void Resize_WiderThanWorkArea_PinsToTheLeftEdgeAndOverhangs()
    {
        Assert.Equal(new ScreenRect(0, 100, 4000, 900), WindowGeometry.Resize(Window, 4000, 800, Work));
    }

    [Fact]
    public void Resize_NoOverflow_LeavesAWindowPartlyOffTheLeftWhereItIs()
    {
        var partlyOff = new ScreenRect(-50, 100, 750, 700);

        Assert.Equal(new ScreenRect(-50, 100, 750, 700), WindowGeometry.Resize(partlyOff, 800, 600, Work));
    }

    [Fact]
    public void Resize_OnSecondMonitor_ClampsAgainstThatWorkArea()
    {
        var onSecond = new ScreenRect(5000, 100, 5700, 700);

        Assert.Equal(new ScreenRect(4760, 100, 5760, 900), WindowGeometry.Resize(onSecond, 1000, 800, SecondWork));
    }

    [Fact]
    public void Halves_EvenWidth_SplitExactly()
    {
        Assert.Equal(new ScreenRect(0, 0, 1920, 2100), WindowGeometry.LeftHalf(Work));
        Assert.Equal(new ScreenRect(1920, 0, 3840, 2100), WindowGeometry.RightHalf(Work));
    }

    [Fact]
    public void Halves_OddWidth_LeftGetsTheFloorRightGetsTheRest()
    {
        var odd = new ScreenRect(0, 0, 3841, 2100);

        Assert.Equal(new ScreenRect(0, 0, 1920, 2100), WindowGeometry.LeftHalf(odd));
        Assert.Equal(new ScreenRect(1920, 0, 3841, 2100), WindowGeometry.RightHalf(odd));
    }

    [Fact]
    public void Halves_OffsetMonitor_TileItsWorkArea()
    {
        var left = WindowGeometry.LeftHalf(SecondWork);
        var right = WindowGeometry.RightHalf(SecondWork);

        Assert.Equal(new ScreenRect(3840, 0, 4800, 1040), left);
        Assert.Equal(new ScreenRect(4800, 0, 5760, 1040), right);
        Assert.Equal(left.Right, right.Left);
    }

    [Fact]
    public void WidthAndHeight_AreEdgeDifferences()
    {
        Assert.Equal(800, WindowGeometry.Width(Window));
        Assert.Equal(600, WindowGeometry.Height(Window));
    }
}
