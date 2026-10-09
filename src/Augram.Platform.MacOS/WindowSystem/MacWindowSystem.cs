using System.Diagnostics;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The macOS <see cref="IWindowSystem"/>. Identity comes from the window server's list (<see cref="MacWindowList"/>,
/// <see cref="MacWindowPick"/>): <see cref="WindowIdentity.Handle"/> and <see cref="WindowIdentity.RootHandle"/> are both the
/// <c>CGWindowID</c>, since a macOS window has no child windows to resolve; the process name is the executable's file
/// name (<c>Safari</c>, <c>Google Chrome</c>), the counterpart of <c>chrome.exe</c>; the class chain is empty. Activation
/// goes through the Accessibility API (raise the window, make the app frontmost), which works from a background app
/// where <c>NSRunningApplication.activate</c> is refused since macOS 14. Every method may block for milliseconds and is
/// called from the engine worker or the command executor, never from a hook handler.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacWindowSystem : IWindowSystem
{
    private const string UnknownProcess = "unknown";

    private readonly MacWindowKeys _keys = new(() => (MacWindowList.OnScreen(), MacWindowList.Displays()), () => Environment.TickCount64, Environment.ProcessId);
    private int _frontmostPid = -1;

    /// <summary>The <c>CGWindowID</c> under the point from a copy of the window list at most 250 ms old (<see cref="MacWindowKeys"/>); the ignore list's watch only.</summary>
    public nint? WindowKeyAt(int x, int y) => (nint)_keys.KeyAt(x, y);

    /// <summary>
    /// The frontmost app's pid (the system-wide focused application, an AX call that asks no app anything), not the focused
    /// window: an ignored app is matched by its executable. A new frontmost app drops the window-list copy behind
    /// <see cref="WindowKeyAt"/>, since its windows usually came to the front with it.
    /// </summary>
    public nint? ForegroundKey()
    {
        var pid = Ax.FocusedApplicationPid();
        if (Interlocked.Exchange(ref _frontmostPid, pid) != pid)
        {
            _keys.Invalidate();
        }

        return pid;
    }

    public WindowIdentity? WindowAt(int x, int y)
    {
        var displays = MacWindowList.Displays();
        var hit = MacWindowPick.At(MacWindowList.OnScreen(), x, y, Environment.ProcessId, displays);
        return hit is null ? null : Identify(hit, displays);
    }

    public WindowIdentity? Foreground()
    {
        var window = Ax.FocusedWindow(out _);
        try
        {
            var id = window == 0 ? null : Ax.WindowId(window);
            var info = id is null ? null : MacWindowList.ById(id.Value);
            return info is null ? null : Identify(info, MacWindowList.Displays());
        }
        finally
        {
            Cf.Release(window);
        }
    }

    /// <summary>A20: nothing to do for the desktop or for a window that already has focus.</summary>
    public ActivationResult Activate(WindowIdentity target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsDesktop || IsFocused((uint)target.RootHandle))
        {
            return ActivationResult.NotNeeded;
        }

        var started = Stopwatch.GetTimestamp();
        var app = Ax.Application(target.ProcessId);
        if (app == 0)
        {
            return new ActivationResult(false, "none", ElapsedMs(started));
        }

        try
        {
            var window = Ax.FindWindow(app, (uint)target.RootHandle, out _);
            try
            {
                if (window != 0)
                {
                    Ax.Perform(window, Ax.RaiseAction);
                    Ax.SetBool(window, Ax.MainAttribute, true);
                }
            }
            finally
            {
                Cf.Release(window);
            }

            var ok = Ax.SetBool(app, Ax.FrontmostAttribute, true) == MacNative.AXErrorSuccess;
            return new ActivationResult(ok, ok ? "ax-frontmost" : "none", ElapsedMs(started));
        }
        finally
        {
            Cf.Release(app);
        }
    }

    private static bool IsFocused(uint windowId)
    {
        var focused = Ax.FocusedWindow(out _);
        try
        {
            return focused != 0 && Ax.WindowId(focused) == windowId;
        }
        finally
        {
            Cf.Release(focused);
        }
    }

    private static WindowIdentity Identify(MacWindowInfo info, IReadOnlyList<MacRect> displays)
    {
        var path = MacWindowList.ProcessPath(info.ProcessId);
        var name = path is null ? info.OwnerName : Path.GetFileName(path);
        return new WindowIdentity(
            (nint)info.Id,
            (nint)info.Id,
            string.IsNullOrEmpty(name) ? UnknownProcess : name,
            path,
            string.IsNullOrEmpty(info.Title) ? null : info.Title,
            [],
            info.ProcessId,
            MacWindowPick.IsFullScreen(info, displays),
            MacWindowPick.IsDesktop(info));
    }

    private static int ElapsedMs(long started) => (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
