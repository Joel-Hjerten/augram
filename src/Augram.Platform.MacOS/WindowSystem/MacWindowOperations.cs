using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The macOS <see cref="IWindowOperations"/> through the Accessibility API (the table in <c>Core/Steps/WindowOp/README.md</c>).
/// The window is found again by its <c>CGWindowID</c> among its app's <c>AXWindows</c>, so a window closed since the
/// stroke reports "window gone" instead of acting on another. Close presses the window's close button (the app runs its
/// own close path, "save changes?" included); Minimize sets <c>AXMinimized</c>; MaximizeOrRestore uses macOS's own window
/// tiling (Joel, 2026-10-09: the green button's Fill): the app's Window › Move &amp; Resize › Fill, and Return to Previous
/// Size to restore, pressed through Accessibility after bringing the window forward, so macOS remembers the size and the
/// app sees what it sees when the user picks Fill. Without those items (an app with no standard Window menu) it fills the
/// visible frame itself and restores the frame it remembered (<see cref="MacMaximize"/>); a filled window with nothing to
/// return to gets a default frame (<see cref="MacMaximize.DefaultRestore"/>). Native full screen is left. Pressing the
/// zoom button, as before 2026-10-09, sent a window into native full screen on current macOS. The placement operations and
/// always-on-top are not built yet and report not supported. Runs on the command executor thread only; nothing here
/// throws, a failed call becomes <see cref="WindowOperationResult.Failed"/> with the reason.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacWindowOperations : IWindowOperations
{
    private const int MaxRemembered = 64;

    // Frames from before a maximize, by window id; only the command executor thread touches it.
    private readonly Dictionary<uint, MacRect> _restore = [];
    private const string Source = "window";

    /// <summary>macOS's Fill (fn-Control-F) and Return to Previous Size (fn-Control-R), by key: their titles are localised.</summary>
    private const string FillKey = "F";
    private const string ReturnKey = "R";

    /// <summary>How long a tiling press may take to move the window (macOS animates it) before it counts as having done nothing.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromMilliseconds(600);
    private const int SettlePoll = 20;

    private readonly IEventLog _log;

    public MacWindowOperations(IEventLog? log = null)
    {
        _log = log ?? NullEventLog.Instance;
    }

    public HostPlatform Platform => HostPlatform.MacOS;

    public bool Supports(WindowOperation operation) =>
        operation is WindowOperation.Close or WindowOperation.Minimize or WindowOperation.MaximizeOrRestore;

    public WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!Supports(operation))
        {
            return WindowOperationResult.NotSupported(operation, Platform);
        }

        if (window.IsDesktop)
        {
            return WindowOperationResult.Failed("the desktop is not a window");
        }

        var app = Ax.Application(window.ProcessId);
        if (app == 0)
        {
            return WindowOperationResult.Failed("process gone");
        }

        try
        {
            var id = (uint)window.RootHandle;
            var element = Ax.FindWindow(app, id, out var error);
            if (element == 0)
            {
                return WindowOperationResult.Failed(Ax.Describe(error));
            }

            try
            {
                return operation switch
                {
                    WindowOperation.Close => Result(Ax.PressButton(element, Ax.CloseButtonAttribute)),
                    WindowOperation.Minimize => Result(Ax.SetBool(element, Ax.MinimizedAttribute, true)),
                    _ => MaximizeOrRestore(app, element, id),
                };
            }
            finally
            {
                Cf.Release(element);
            }
        }
        finally
        {
            Cf.Release(app);
        }
    }

    /// <summary>Polls the window's frame for up to <see cref="SettleTimeout"/>: true once it differs from <paramref name="before"/> by more than a point.</summary>
    private static bool FrameChanged(nint window, MacRect before)
    {
        var deadline = Environment.TickCount64 + (long)SettleTimeout.TotalMilliseconds;
        do
        {
            if (Ax.Frame(window) is { } now && !now.IsNear(before, 1))
            {
                return true;
            }

            Thread.Sleep(SettlePoll);
        }
        while (Environment.TickCount64 < deadline);

        return false;
    }

    private static string Describe(MacRect rect) => $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}";

    private static WindowOperationResult Result(int error) =>
        error == MacNative.AXErrorSuccess ? WindowOperationResult.Ok : WindowOperationResult.Failed(Ax.Describe(error));

    private WindowOperationResult MaximizeOrRestore(nint app, nint element, uint id)
    {
        if (Ax.GetBool(element, Ax.FullScreenAttribute) == true)
        {
            return Result(Ax.SetBool(element, Ax.FullScreenAttribute, false));
        }

        if (Ax.Frame(element) is not { } frame)
        {
            return WindowOperationResult.Failed("could not read the window's frame");
        }

        var screens = MacScreens.All();
        if (screens.Count == 0)
        {
            return WindowOperationResult.Failed("no screen");
        }

        var visible = screens[MacMaximize.ScreenFor(frame, [.. screens.Select(screen => screen.Frame)])].Visible;
        if (MacMaximize.Fills(frame, visible))
        {
            // Augram filled it itself (an app without the tiling items): its remembered frame.
            if (_restore.Remove(id, out var previous))
            {
                return Result(Ax.SetFrame(element, previous));
            }

            if (PressTiling(app, element, frame, ReturnKey, "Return to Previous Size") is { } returned)
            {
                return returned;
            }

            // macOS's own zoom (a title-bar double-click, Option-click on green): Zoom again toggles it back to the frame
            // macOS remembered (Joel, 2026-10-09, VS Code). An app that grew its window itself may count as zoomed already
            // and not move: then the default below.
            if (PressMenuItem(app, element, frame, Ax.FindZoomMenuItem(app), "Zoom", []) is { } unzoomed)
            {
                return unzoomed;
            }

            var fallback = MacMaximize.DefaultRestore(visible);
            _log.Info(Source, "Restored a window Augram did not maximize", ("frame", Describe(frame)), ("visible", Describe(visible)), ("restoredTo", Describe(fallback)));
            return Result(Ax.SetFrame(element, fallback));
        }

        if (PressTiling(app, element, frame, FillKey, "Fill") is { } filled)
        {
            return filled;
        }

        if (_restore.Count >= MaxRemembered)
        {
            _restore.Clear();
        }

        _restore[id] = frame;
        return Result(Ax.SetFrame(element, visible));
    }

    /// <summary>
    /// Brings the window forward (the menu acts on the app's main window, which need not be the one under the gesture) and
    /// presses the Window menu item with this shortcut, then waits for THIS window's frame to change (the Eyeris agent's
    /// check, 2026-10-09). Null when the app has no such enabled item, or the press moved nothing, so the caller falls back.
    /// One log line either way.
    /// </summary>
    private WindowOperationResult? PressTiling(nint app, nint window, MacRect before, string key, string what)
    {
        var seen = new List<string>();
        return PressMenuItem(app, window, before, Ax.FindWindowMenuItem(app, key, Ax.ControlModifier | Ax.NoCommandModifier, seen), what, seen);
    }

    /// <summary>Presses <paramref name="item"/> (owned, released here; zero = not found) on the window brought forward, and reports whether THIS window moved.</summary>
    private WindowOperationResult? PressMenuItem(nint app, nint window, MacRect before, nint item, string what, List<string> seen)
    {
        try
        {
            if (item == 0)
            {
                _log.Info(Source, "No such Window menu item", ("item", what), ("windowMenu", seen.Count == 0 ? "not read" : string.Join("; ", seen)));
                return null;
            }

            Ax.SetBool(app, Ax.FrontmostAttribute, true);
            Ax.SetBool(window, Ax.MainAttribute, true);
            Ax.Perform(window, Ax.RaiseAction);
            var pressed = Ax.Perform(item, Ax.PressAction);
            var moved = pressed == MacNative.AXErrorSuccess && FrameChanged(window, before);
            _log.Info(Source, "Pressed a Window menu item", ("item", what), ("result", Ax.Describe(pressed)), ("windowChanged", moved));
            return moved ? WindowOperationResult.Ok : null;
        }
        finally
        {
            Cf.Release(item);
            Ax.ReleaseMenuSearch();
        }
    }
}
