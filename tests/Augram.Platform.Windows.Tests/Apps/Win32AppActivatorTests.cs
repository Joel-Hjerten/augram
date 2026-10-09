using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.Windows.Apps;
using Augram.Platform.Windows.Tests.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.Apps;

/// <summary>The Open app step on Windows over the scripted window tree: which window is the app's, restore, activation, and "not running".</summary>
public sealed class Win32AppActivatorTests
{
    [Fact]
    public void TheFrontmostMainWindowOfTheApp_IsBroughtForward()
    {
        var win = Chrome(new FakeWin32 { Foreground = 0x90, PlainSucceeds = true });

        var result = Activator(win).BringToFront("CHROME.EXE");

        Assert.Equal(AppActivationOutcome.Activated, result.Outcome);
        Assert.Equal(0x20, win.Foreground);
        Assert.DoesNotContain(win.Calls, call => call.StartsWith("show:", StringComparison.Ordinal));
    }

    [Fact]
    public void AMinimizedWindow_IsRestoredFirst()
    {
        var win = Chrome(new FakeWin32 { Foreground = 0x90, PlainSucceeds = true }).WithState(0x20, iconic: true);

        Assert.Equal(AppActivationOutcome.Activated, Activator(win).BringToFront("chrome.exe").Outcome);
        Assert.Equal("show:32:9", win.Calls[0]);
    }

    [Fact]
    public void HiddenOwnedAndToolWindows_DoNotCount_AndNoWindowMeansNotRunning()
    {
        var win = new FakeWin32 { PlainSucceeds = true }
            .AddWindow(0x10, "Hidden", pid: 7, visible: false)
            .AddWindow(0x11, "Palette", pid: 7)
            .AddWindow(0x12, "#32770", owner: 0x50, pid: 7)
            .AddWindow(0x50, "Other", pid: 8)
            .AddProcess(7, @"C:\Apps\tool.exe")
            .AddProcess(8, @"C:\Apps\other.exe");
        win.ToolWindows.Add(0x11);

        Assert.Equal(AppActivationOutcome.NotRunning, Activator(win).BringToFront("tool.exe").Outcome);
        Assert.Equal(AppActivationOutcome.NotRunning, Activator(win).BringToFront("notepad.exe").Outcome);
    }

    [Fact]
    public void WhenWindowsRefusesTheForeground_ItFailsWithTheReason()
    {
        var win = Chrome(new FakeWin32 { Foreground = 0x90 });

        var result = Activator(win).BringToFront(@"C:\Program Files\Google\Chrome\Application\chrome.exe");

        Assert.Equal(AppActivationOutcome.Failed, result.Outcome);
        Assert.Contains("chrome.exe", result.Reason, StringComparison.Ordinal);
    }

    /// <summary>Front to back: a tool window and a hidden window of Chrome, then its main window, then a second main window behind it.</summary>
    private static FakeWin32 Chrome(FakeWin32 win)
    {
        win.AddWindow(0x90, "Notepad", pid: 3)
            .AddWindow(0x10, "Chrome_WidgetWin_2", pid: 42)
            .AddWindow(0x11, "Chrome_WidgetWin_0", pid: 42, visible: false)
            .AddWindow(0x20, "Chrome_WidgetWin_1", pid: 42, title: "Augram - Google Chrome")
            .AddWindow(0x21, "Chrome_WidgetWin_1", pid: 42, title: "Inbox - Google Chrome")
            .AddProcess(42, @"C:\Program Files\Google\Chrome\Application\chrome.exe")
            .AddProcess(3, @"C:\Windows\notepad.exe");
        win.ToolWindows.Add(0x10);
        return win;
    }

    private static Win32AppActivator Activator(FakeWin32 win) => new(win, win, win, NullEventLog.Instance, _ => { });
}
