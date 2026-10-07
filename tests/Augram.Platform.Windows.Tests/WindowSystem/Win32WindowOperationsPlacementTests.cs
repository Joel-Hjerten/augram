using Augram.Core.Abstractions;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>
/// Center, SetSize and the two snaps over the fake: restore first only when zoomed or iconic, the rectangle read
/// after the restore, the bounds handed to SetWindowPos, and failure reasons with error codes.
/// </summary>
public sealed class Win32WindowOperationsPlacementTests
{
    private const nint Root = 0x10;
    private const int SwRestore = 9;
    private static readonly ScreenRect Monitor = new(0, 0, 3840, 2160);
    private static readonly ScreenRect Work = new(0, 0, 3840, 2100);
    private static readonly ScreenRect Normal = new(100, 100, 900, 700);
    private static readonly ScreenRect Maximized = new(-8, -8, 3848, 2108);

    [Fact]
    public void Center_NormalWindow_MovesToTheWorkAreaCentreWithoutRestoring()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.Center, Identity());

        Assert.True(result.Succeeded);
        Assert.Equal(["bounds:16:1520,750,800x600"], win.Calls);
    }

    [Fact]
    public void Center_Zoomed_RestoresFirstAndCentresTheRestoredRect()
    {
        var win = Setup(zoomed: true);

        Operations(win).Perform(WindowOperation.Center, Identity());

        Assert.Equal([$"show:16:{SwRestore}", "bounds:16:1520,750,800x600"], win.Calls);
    }

    [Fact]
    public void Center_Iconic_RestoresFirst()
    {
        var win = Setup(iconic: true);

        Operations(win).Perform(WindowOperation.Center, Identity());

        Assert.Equal([$"show:16:{SwRestore}", "bounds:16:1520,750,800x600"], win.Calls);
    }

    [Fact]
    public void SetSize_NoSize_Fails()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.SetSize, Identity());

        Assert.Equal("no size", result.Reason);
        Assert.Empty(win.Calls);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, -1)]
    public void SetSize_NonPositive_Fails(int width, int height)
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.SetSize, Identity(), new WindowSize(width, height));

        Assert.False(result.Succeeded);
        Assert.Contains("size must be positive", result.Reason, StringComparison.Ordinal);
        Assert.Empty(win.Calls);
    }

    [Fact]
    public void SetSize_KeepsTheTopLeftCorner()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.SetSize, Identity(), new WindowSize(1000, 800));

        Assert.True(result.Succeeded);
        Assert.Equal(["bounds:16:100,100,1000x800"], win.Calls);
    }

    [Fact]
    public void SetSize_WouldLeaveTheWorkArea_ShiftsBackInside()
    {
        var win = Setup(window: new ScreenRect(3000, 1500, 3800, 2100));

        Operations(win).Perform(WindowOperation.SetSize, Identity(), new WindowSize(1000, 800));

        Assert.Equal(["bounds:16:2840,1300,1000x800"], win.Calls);
    }

    [Fact]
    public void SetSize_Zoomed_RestoresFirstAndKeepsTheRestoredCorner()
    {
        var win = Setup(zoomed: true);

        Operations(win).Perform(WindowOperation.SetSize, Identity(), new WindowSize(1000, 800));

        Assert.Equal([$"show:16:{SwRestore}", "bounds:16:100,100,1000x800"], win.Calls);
    }

    [Fact]
    public void SetSize_SetWindowPosFails_ReasonCarriesTheErrorCode()
    {
        var win = Setup();
        win.SetWindowPosFails = true;
        win.Error = 5;

        var result = Operations(win).Perform(WindowOperation.SetSize, Identity(), new WindowSize(1000, 800));

        Assert.Equal("SetWindowPos failed (5)", result.Reason);
    }

    [Fact]
    public void SnapLeftHalf_FillsTheLeftHalfOfTheWorkArea()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.SnapLeftHalf, Identity());

        Assert.True(result.Succeeded);
        Assert.Equal(["bounds:16:0,0,1920x2100"], win.Calls);
    }

    [Fact]
    public void SnapRightHalf_FillsTheRightHalfOfTheWorkArea()
    {
        var win = Setup();

        Operations(win).Perform(WindowOperation.SnapRightHalf, Identity());

        Assert.Equal(["bounds:16:1920,0,1920x2100"], win.Calls);
    }

    [Theory]
    [InlineData(WindowOperation.SnapLeftHalf, "bounds:16:0,0,1920x2100")]
    [InlineData(WindowOperation.SnapRightHalf, "bounds:16:1920,0,1920x2100")]
    public void Snap_Zoomed_RestoresFirst(WindowOperation operation, string bounds)
    {
        var win = Setup(zoomed: true);

        Operations(win).Perform(operation, Identity());

        Assert.Equal([$"show:16:{SwRestore}", bounds], win.Calls);
    }

    [Theory]
    [InlineData(WindowOperation.Center)]
    [InlineData(WindowOperation.SnapLeftHalf)]
    public void Placement_NoMonitor_Fails(WindowOperation operation)
    {
        var win = new FakeWin32().AddWindow(Root, "App", pid: 1);

        var result = Operations(win).Perform(operation, Identity());

        Assert.False(result.Succeeded);
        Assert.Equal("no monitor for the window", result.Reason);
        Assert.Empty(win.Calls);
    }

    private static FakeWin32 Setup(bool zoomed = false, bool iconic = false, ScreenRect? window = null)
        => new FakeWin32()
            .AddWindow(Root, "App", pid: 1)
            .WithRects(Root, window ?? Normal, Monitor, Work)
            .WithState(Root, iconic, zoomed, maximizedRect: Maximized);

    private static Win32WindowOperations Operations(FakeWin32 win) => new(win, win);

    private static WindowIdentity Identity() => new(Root, Root, "app.exe", null, null, [], 1, false, false);
}
