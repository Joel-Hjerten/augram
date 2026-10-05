using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>Identity assembly over a scripted window tree: class chain, process, flags.</summary>
public sealed class WindowIdentityReaderTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 3840, 2160);

    [Fact]
    public void ChromeChildControl_ReadsChainProcessAndTitle()
    {
        var win = new FakeWin32()
            .AddWindow(0x10, "Chrome_WidgetWin_1", pid: 42, title: "Augram - Google Chrome")
            .AddWindow(0x11, "Chrome_RenderWidgetHostHWND", parent: 0x10, root: 0x10, pid: 42)
            .AddProcess(42, @"C:\Program Files\Google\Chrome\Application\chrome.exe")
            .WithRects(0x10, new ScreenRect(-8, -8, 3848, 2112), Monitor);

        var id = new WindowIdentityReader(win).Read(0x11);

        Assert.NotNull(id);
        Assert.Equal(0x11, id.Handle);
        Assert.Equal(0x10, id.RootHandle);
        Assert.Equal("chrome.exe", id.ProcessName);
        Assert.Equal(@"C:\Program Files\Google\Chrome\Application\chrome.exe", id.ProcessPath);
        Assert.Equal("Augram - Google Chrome", id.Title);
        Assert.Equal(["Chrome_RenderWidgetHostHWND", "Chrome_WidgetWin_1"], id.ClassChain);
        Assert.Equal(42, id.ProcessId);
        Assert.False(id.IsFullScreen);
        Assert.False(id.IsDesktop);
    }

    [Fact]
    public void DesktopIconView_IsDesktopAndNeverFullScreen()
    {
        var win = new FakeWin32()
            .AddWindow(0x20, "WorkerW", pid: 7)
            .AddWindow(0x21, "SHELLDLL_DefView", parent: 0x20, root: 0x20, pid: 7)
            .AddWindow(0x22, "SysListView32", parent: 0x21, root: 0x20, pid: 7)
            .AddProcess(7, @"C:\Windows\explorer.exe")
            .WithRects(0x20, Monitor, Monitor);

        var id = new WindowIdentityReader(win).Read(0x22);

        Assert.NotNull(id);
        Assert.True(id.IsDesktop);
        Assert.False(id.IsFullScreen);
        Assert.Equal(["SysListView32", "SHELLDLL_DefView", "WorkerW"], id.ClassChain);
        Assert.Equal("explorer.exe", id.ProcessName);
    }

    [Fact]
    public void ShellWindowHandle_IsDesktopEvenWithUnknownClass()
    {
        var win = new FakeWin32 { Shell = 0x30 }.AddWindow(0x30, "Progman", pid: 7).AddProcess(7, @"C:\Windows\explorer.exe");

        var id = new WindowIdentityReader(win).Read(0x30);

        Assert.True(id!.IsDesktop);
    }

    [Fact]
    public void BorderlessGame_IsFullScreen()
    {
        var win = new FakeWin32()
            .AddWindow(0x40, "UnityWndClass", pid: 99, title: "Game")
            .AddProcess(99, @"D:\Games\game.exe")
            .WithRects(0x40, Monitor, Monitor);

        var id = new WindowIdentityReader(win).Read(0x40);

        Assert.True(id!.IsFullScreen);
        Assert.False(id.IsDesktop);
    }

    [Fact]
    public void UwpFrame_TakesProcessFromCoreWindow()
    {
        var win = new FakeWin32()
            .AddWindow(0x50, UwpHostRule.FrameClass, pid: 500, title: "Calculator")
            .AddWindow(0x51, UwpHostRule.CoreWindowClass, parent: 0x50, root: 0x50, pid: 501)
            .AddProcess(500, @"C:\Windows\System32\ApplicationFrameHost.exe")
            .AddProcess(501, @"C:\Program Files\WindowsApps\Calculator\CalculatorApp.exe");

        var id = new WindowIdentityReader(win).Read(0x50);

        Assert.Equal("CalculatorApp.exe", id!.ProcessName);
        Assert.Equal(501, id.ProcessId);
        Assert.Equal(0x50, id.RootHandle);
        Assert.Equal("Calculator", id.Title);
    }

    [Fact]
    public void ProtectedProcess_FallsBackToBaseName()
    {
        var win = new FakeWin32().AddWindow(0x60, "X", pid: 4).AddProcess(4, null, "elevated");

        var id = new WindowIdentityReader(win).Read(0x60);

        Assert.Equal("elevated.exe", id!.ProcessName);
        Assert.Null(id.ProcessPath);
    }

    [Fact]
    public void UnknownProcess_StillNamesSomething()
    {
        var win = new FakeWin32().AddWindow(0x70, "X");

        var id = new WindowIdentityReader(win).Read(0x70);

        Assert.Equal("unknown", id!.ProcessName);
        Assert.Null(id.Title);
    }

    [Fact]
    public void ZeroHandle_IsNull()
    {
        Assert.Null(new WindowIdentityReader(new FakeWin32()).Read(0));
    }

    [Fact]
    public void OwnedPopup_RootsAtTheOwner()
    {
        var win = new FakeWin32()
            .AddWindow(0x80, "Notepad++", pid: 1, title: "Editor")
            .AddWindow(0x81, "#32770", root: 0x81, owner: 0x80, pid: 1, title: "Find")
            .AddProcess(1, @"C:\npp\notepad++.exe");

        var id = new WindowIdentityReader(win).Read(0x81);

        Assert.Equal(0x80, id!.RootHandle);
        Assert.Equal("Editor", id.Title);
        Assert.Equal(["#32770", "Notepad++"], id.ClassChain);
    }
}
