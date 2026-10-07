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

    public WindowIdentity? WindowAt(int x, int y)
    {
        var hit = MacWindowPick.At(MacWindowList.OnScreen(), x, y, Environment.ProcessId);
        return hit is null ? null : Identify(hit);
    }

    public WindowIdentity? Foreground()
    {
        var window = Ax.FocusedWindow(out _);
        try
        {
            var id = window == 0 ? null : Ax.WindowId(window);
            var info = id is null ? null : MacWindowList.ById(id.Value);
            return info is null ? null : Identify(info);
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

    private static WindowIdentity Identify(MacWindowInfo info)
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
            MacWindowPick.IsFullScreen(info, MacWindowList.Displays()),
            MacWindowPick.IsDesktop(info));
    }

    private static int ElapsedMs(long started) => (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
