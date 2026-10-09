namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// What has focus, system-wide: the focused application and its focused window. Both start from the system-wide element's
/// focused application; its pid is read locally, so <see cref="FocusedApplicationPid"/> asks no other app anything (the
/// ignore list's watch polls it), while <see cref="FocusedWindow"/> asks the focused app for its window.
/// <para>
/// The system-wide query runs on the main thread (<see cref="MainThread"/>), always. When Augram itself is the focused
/// app, the Accessibility API answers it in-process on the calling thread, through AppKit and Avalonia's window
/// (<c>-[AvnWindow accessibilityFocusedUIElement]</c>); off the main thread that crashed the installed 0.5.1 three times
/// at start on 2026-10-09 (EXC_BAD_ACCESS in <c>-[AvnWindow automationPeer]</c> on <c>augram-ignore-watch</c>, the
/// window just shown by a take-over). Whether the answer is Augram is only known after asking, so every call hops.
/// </para>
/// </summary>
internal static partial class Ax
{
    /// <summary>The focused window of the focused application, system-wide. Owned; zero when there is none.</summary>
    public static nint FocusedWindow(out int pid)
    {
        pid = 0;
        var app = FocusedApplication();
        if (app == 0)
        {
            return 0;
        }

        try
        {
            MacNative.AXUIElementGetPid(app, out pid);
            return Copy(app, Cf.Constant(FocusedWindowAttribute), out var window) == MacNative.AXErrorSuccess ? window : 0;
        }
        finally
        {
            Cf.Release(app);
        }
    }

    /// <summary>The pid of the focused application; 0 when there is none or Accessibility is not granted.</summary>
    public static int FocusedApplicationPid()
    {
        var app = FocusedApplication();
        if (app == 0)
        {
            return 0;
        }

        try
        {
            return MacNative.AXUIElementGetPid(app, out var pid) == MacNative.AXErrorSuccess ? pid : 0;
        }
        finally
        {
            Cf.Release(app);
        }
    }

    /// <summary>The focused application's element, from the system-wide element with the messaging timeout set. Owned; zero when there is none.</summary>
    private static nint FocusedApplication()
    {
        var system = MacNative.AXUIElementCreateSystemWide();
        if (system == 0)
        {
            return 0;
        }

        try
        {
            MacNative.AXUIElementSetMessagingTimeout(system, MessagingTimeoutSeconds);
            var attribute = Cf.Constant(FocusedApplicationAttribute);
            return MainThread.TryInvoke(
                () => MacNative.AXUIElementCopyAttributeValue(system, attribute, out var app) == MacNative.AXErrorSuccess ? app : 0,
                out var focused)
                ? focused
                : 0;
        }
        finally
        {
            Cf.Release(system);
        }
    }
}
