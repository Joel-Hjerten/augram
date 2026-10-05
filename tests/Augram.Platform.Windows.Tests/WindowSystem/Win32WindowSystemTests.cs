using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.Windows.Interop;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>The public adapter: A20 gating over the fake, plus one real-Win32 smoke test at the cursor.</summary>
public sealed class Win32WindowSystemTests
{
    [Fact]
    public void Activate_TargetAlreadyForeground_NotNeeded()
    {
        var win = new FakeWin32 { Foreground = 0x11 }
            .AddWindow(0x10, "App", pid: 1)
            .AddWindow(0x11, "Child", parent: 0x10, root: 0x10, pid: 1);
        var system = new Win32WindowSystem(win, win, NullEventLog.Instance, _ => { });

        var result = system.Activate(new WindowIdentity(0x10, 0x10, "app.exe", null, null, [], 1, false, false));

        Assert.Same(ActivationResult.NotNeeded, result);
        Assert.Empty(win.Calls);
    }

    [Fact]
    public void Activate_Desktop_NotNeeded()
    {
        var win = new FakeWin32 { Foreground = 0x20 }.AddWindow(0x20, "App", pid: 1);
        var system = new Win32WindowSystem(win, win, NullEventLog.Instance, _ => { });

        var result = system.Activate(new WindowIdentity(0x30, 0x30, "explorer.exe", null, null, ["Progman"], 2, false, true));

        Assert.Same(ActivationResult.NotNeeded, result);
        Assert.Empty(win.Calls);
    }

    [Fact]
    public void Activate_OtherWindow_RunsTechniques()
    {
        var win = new FakeWin32 { Foreground = 0x20, PlainSucceeds = true }
            .AddWindow(0x10, "App", pid: 1)
            .AddWindow(0x20, "Other", pid: 2);
        var system = new Win32WindowSystem(win, win, NullEventLog.Instance, _ => { });

        var result = system.Activate(new WindowIdentity(0x10, 0x10, "app.exe", null, null, [], 1, false, false));

        Assert.True(result.Succeeded);
        Assert.Equal(0x10, win.Foreground);
    }

    [Fact]
    public void WindowAt_Cursor_ReturnsSaneIdentityOrNull()
    {
        if (!Environment.UserInteractive || !OperatingSystem.IsWindows() || !NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var system = new Win32WindowSystem(NullEventLog.Instance);

        var id = system.WindowAt(cursor.X, cursor.Y);

        if (id is null)
        {
            return;
        }

        Assert.NotEqual(0, id.Handle);
        Assert.NotEqual(0, id.RootHandle);
        Assert.False(string.IsNullOrEmpty(id.ProcessName));
        Assert.NotEmpty(id.ClassChain);
        Assert.False(id.IsDesktop && id.IsFullScreen);
    }
}
