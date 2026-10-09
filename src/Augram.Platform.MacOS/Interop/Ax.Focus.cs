namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// What has focus, system-wide: the focused application and its focused window. Both start from the system-wide element's
/// focused application; its pid is read locally, so <see cref="FocusedApplicationPid"/> asks no app anything (the ignore
/// list's watch polls it), while <see cref="FocusedWindow"/> asks the focused app for its window.
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
            return Copy(system, Cf.Constant(FocusedApplicationAttribute), out var app) == MacNative.AXErrorSuccess ? app : 0;
        }
        finally
        {
            Cf.Release(system);
        }
    }
}
