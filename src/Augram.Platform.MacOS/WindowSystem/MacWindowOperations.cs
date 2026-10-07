using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The macOS <see cref="IWindowOperations"/> through the Accessibility API (the table in <c>Core/Steps/WindowOp/README.md</c>).
/// The window is found again by its <c>CGWindowID</c> among its app's <c>AXWindows</c>, so a window closed since the
/// stroke reports "window gone" instead of acting on another. Close presses the window's close button (the app runs its
/// own close path, "save changes?" included); Minimize sets <c>AXMinimized</c>; MaximizeOrRestore fills the screen's
/// visible frame and restores the frame it had (<see cref="MacMaximize"/>), leaves native full screen when the window is
/// in it, and presses the zoom button to restore a window something else filled. The placement operations and
/// always-on-top are not built yet and report not supported. Runs on the command executor thread only; nothing here
/// throws, a failed call becomes <see cref="WindowOperationResult.Failed"/> with the reason.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacWindowOperations : IWindowOperations
{
    private const int MaxRemembered = 64;

    // Frames from before a maximize, by window id; only the command executor thread touches it.
    private readonly Dictionary<uint, MacRect> _restore = [];

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
                    _ => MaximizeOrRestore(element, id),
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

    private static WindowOperationResult Result(int error) =>
        error == MacNative.AXErrorSuccess ? WindowOperationResult.Ok : WindowOperationResult.Failed(Ax.Describe(error));

    private WindowOperationResult MaximizeOrRestore(nint element, uint id)
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
            return _restore.Remove(id, out var previous)
                ? Result(Ax.SetFrame(element, previous))
                : Result(Ax.PressButton(element, Ax.ZoomButtonAttribute));
        }

        if (_restore.Count >= MaxRemembered)
        {
            _restore.Clear();
        }

        _restore[id] = frame;
        return Result(Ax.SetFrame(element, visible));
    }
}
