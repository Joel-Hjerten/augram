using System.Runtime.Versioning;
using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// Accessibility API helpers over <see cref="MacNative"/>. Elements returned as <c>nint</c> by a method whose name
/// says it owns them (<see cref="Application"/>, <see cref="FindWindow"/>, <see cref="FocusedWindow"/>) are released
/// by the caller with <see cref="Cf.Release"/>. Every call talks to the target app over IPC; the messaging timeout set
/// on each application element keeps a hung app from holding the calling thread for the system default of 6 s.
/// Every method returns an <c>AXError</c> or a null value instead of throwing; <see cref="Describe"/> turns the error
/// into the reason the log shows. Calls on Augram's own elements go through <see cref="MainThread"/> (see <see cref="IsOwn"/>).
/// </summary>
[SupportedOSPlatform("macos")]
internal static class Ax
{
    public const string WindowsAttribute = "AXWindows";
    public const string FocusedApplicationAttribute = "AXFocusedApplication";
    public const string FocusedWindowAttribute = "AXFocusedWindow";
    public const string FrontmostAttribute = "AXFrontmost";
    public const string MainAttribute = "AXMain";
    public const string MinimizedAttribute = "AXMinimized";
    public const string FullScreenAttribute = "AXFullScreen";
    public const string PositionAttribute = "AXPosition";
    public const string SizeAttribute = "AXSize";
    public const string TitleAttribute = "AXTitle";
    public const string CloseButtonAttribute = "AXCloseButton";
    public const string ZoomButtonAttribute = "AXZoomButton";
    public const string PressAction = "AXPress";
    public const string RaiseAction = "AXRaise";

    public const float MessagingTimeoutSeconds = 0.5f;

    /// <summary>Whether this process (or the app that launched it, for a process started from a terminal) holds the Accessibility permission.</summary>
    public static bool IsTrusted => MacNative.AXIsProcessTrusted() != 0;

    /// <summary>The application element for <paramref name="pid"/>, with the messaging timeout set. Owned.</summary>
    public static nint Application(int pid)
    {
        var app = MacNative.AXUIElementCreateApplication(pid);
        if (app != 0)
        {
            MacNative.AXUIElementSetMessagingTimeout(app, MessagingTimeoutSeconds);
        }

        return app;
    }

    public static bool? GetBool(nint element, string attribute)
    {
        if (Copy(element, Cf.Constant(attribute), out var value) != MacNative.AXErrorSuccess)
        {
            return null;
        }

        try
        {
            return Cf.ReadBool(value);
        }
        finally
        {
            Cf.Release(value);
        }
    }

    public static string? GetString(nint element, string attribute)
    {
        if (Copy(element, Cf.Constant(attribute), out var value) != MacNative.AXErrorSuccess)
        {
            return null;
        }

        try
        {
            return Cf.ReadString(value);
        }
        finally
        {
            Cf.Release(value);
        }
    }

    public static int SetBool(nint element, string attribute, bool value) =>
        Set(element, Cf.Constant(attribute), value ? Cf.True : Cf.False);

    /// <summary>The window's outer frame in global top-left points, as <c>AXPosition</c> and <c>AXSize</c> report it.</summary>
    public static MacRect? Frame(nint window)
    {
        if (Copy(window, Cf.Constant(PositionAttribute), out var position) != MacNative.AXErrorSuccess)
        {
            return null;
        }

        try
        {
            if (MacNative.AXValueGetPoint(position, MacNative.AXValueCGPointType, out var origin) == 0
                || Copy(window, Cf.Constant(SizeAttribute), out var sizeValue) != MacNative.AXErrorSuccess)
            {
                return null;
            }

            try
            {
                return MacNative.AXValueGetSize(sizeValue, MacNative.AXValueCGSizeType, out var size) == 0
                    ? null
                    : new MacRect(origin.X, origin.Y, size.Width, size.Height);
            }
            finally
            {
                Cf.Release(sizeValue);
            }
        }
        finally
        {
            Cf.Release(position);
        }
    }

    /// <summary>
    /// Moves, sizes, then moves again: an app clips a size its current position cannot hold (the far edge would leave the
    /// screen), and a move across screens can change the size the app allows, so the second move settles both.
    /// </summary>
    public static int SetFrame(nint window, MacRect frame)
    {
        var error = SetPosition(window, frame.X, frame.Y);
        if (error != MacNative.AXErrorSuccess)
        {
            return error;
        }

        error = SetSize(window, frame.Width, frame.Height);
        return error != MacNative.AXErrorSuccess ? error : SetPosition(window, frame.X, frame.Y);
    }

    public static int Perform(nint element, string action) => Act(element, Cf.Constant(action));

    private static int SetPosition(nint element, double x, double y)
    {
        var value = MacNative.AXValueCreatePoint(MacNative.AXValueCGPointType, new MacNative.CGPoint { X = x, Y = y });
        try
        {
            return Set(element, Cf.Constant(PositionAttribute), value);
        }
        finally
        {
            Cf.Release(value);
        }
    }

    private static int SetSize(nint element, double width, double height)
    {
        var value = MacNative.AXValueCreateSize(MacNative.AXValueCGSizeType, new MacNative.CGSize { Width = width, Height = height });
        try
        {
            return Set(element, Cf.Constant(SizeAttribute), value);
        }
        finally
        {
            Cf.Release(value);
        }
    }

    /// <summary>Presses one of the window's title-bar buttons (<see cref="CloseButtonAttribute"/>, <see cref="ZoomButtonAttribute"/>).</summary>
    public static int PressButton(nint window, string buttonAttribute)
    {
        var error = Copy(window, Cf.Constant(buttonAttribute), out var button);
        if (error != MacNative.AXErrorSuccess)
        {
            return error;
        }

        try
        {
            return Perform(button, PressAction);
        }
        finally
        {
            Cf.Release(button);
        }
    }

    /// <summary>The <c>CGWindowID</c> of a window element; null when the element is not a window.</summary>
    public static uint? WindowId(nint element) =>
        MacNative.AXUIElementGetWindow(element, out var id) == MacNative.AXErrorSuccess && id != 0 ? id : null;

    /// <summary>The window of <paramref name="app"/> whose <c>CGWindowID</c> is <paramref name="windowId"/>. Owned; zero with the error when absent.</summary>
    public static nint FindWindow(nint app, uint windowId, out int error)
    {
        error = Copy(app, Cf.Constant(WindowsAttribute), out var windows);
        if (error != MacNative.AXErrorSuccess)
        {
            return 0;
        }

        try
        {
            var count = MacNative.CFArrayGetCount(windows);
            for (nint i = 0; i < count; i++)
            {
                var window = MacNative.CFArrayGetValueAtIndex(windows, i);
                if (WindowId(window) == windowId)
                {
                    return MacNative.CFRetain(window);
                }
            }

            error = MacNative.AXErrorInvalidUIElement;
            return 0;
        }
        finally
        {
            Cf.Release(windows);
        }
    }

    /// <summary>The focused window of the focused application, system-wide. Owned; zero when there is none.</summary>
    public static nint FocusedWindow(out int pid)
    {
        pid = 0;
        var system = MacNative.AXUIElementCreateSystemWide();
        if (system == 0)
        {
            return 0;
        }

        try
        {
            MacNative.AXUIElementSetMessagingTimeout(system, MessagingTimeoutSeconds);
            if (Copy(system, Cf.Constant(FocusedApplicationAttribute), out var app) != MacNative.AXErrorSuccess)
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
        finally
        {
            Cf.Release(system);
        }
    }

    /// <summary>
    /// AX calls on Augram's own elements are not IPC: AppKit answers them in-process on the calling thread, and AppKit must run
    /// on the main thread (2026-10-07: maximizing Augram's own window from the command executor hung the executor for good).
    /// </summary>
    private static bool IsOwn(nint element) =>
        MacNative.AXUIElementGetPid(element, out var pid) == MacNative.AXErrorSuccess && pid == Environment.ProcessId;

    private static int Copy(nint element, nint attribute, out nint value)
    {
        if (!IsOwn(element))
        {
            return MacNative.AXUIElementCopyAttributeValue(element, attribute, out value);
        }

        var ran = MainThread.TryInvoke(
            () =>
            {
                var result = MacNative.AXUIElementCopyAttributeValue(element, attribute, out var v);
                return (result, v);
            },
            out var copied);
        value = ran ? copied.v : 0;
        return ran ? copied.result : MacNative.AXErrorCannotComplete;
    }

    private static int Set(nint element, nint attribute, nint value) => IsOwn(element)
        ? OnMain(() => MacNative.AXUIElementSetAttributeValue(element, attribute, value))
        : MacNative.AXUIElementSetAttributeValue(element, attribute, value);

    private static int Act(nint element, nint action) => IsOwn(element)
        ? OnMain(() => MacNative.AXUIElementPerformAction(element, action))
        : MacNative.AXUIElementPerformAction(element, action);

    private static int OnMain(Func<int> call) => MainThread.TryInvoke(call, out var error) ? error : MacNative.AXErrorCannotComplete;

    /// <summary>The reason for the log: what an <c>AXError</c> means for a window operation.</summary>
    public static string Describe(int error) => error switch
    {
        MacNative.AXErrorApiDisabled => "Accessibility permission missing",
        MacNative.AXErrorInvalidUIElement => "window gone",
        MacNative.AXErrorCannotComplete => "the app did not answer",
        MacNative.AXErrorAttributeUnsupported or MacNative.AXErrorActionUnsupported => "the window does not offer it",
        MacNative.AXErrorNoValue => "the window has no such button",
        _ => $"AXError {error}",
    };
}
