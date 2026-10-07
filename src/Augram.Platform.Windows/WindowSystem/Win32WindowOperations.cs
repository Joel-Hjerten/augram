using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The Windows <see cref="IWindowOperations"/> (F5 window actions). Every operation targets
/// <see cref="WindowIdentity.RootHandle"/> and starts with <c>IsWindow</c>, so a window closed since the stroke
/// began reports "window gone" instead of acting on a recycled handle. Close posts what the window's own close
/// button sends; the placement operations restore a maximized or minimized window first, then read its rectangle
/// and the work area and let <see cref="WindowGeometry"/> decide the bounds. Runs on the command executor thread:
/// <c>ShowWindow</c> and <c>SetWindowPos</c> wait for the target thread, so this must never run on the hook thread.
/// Nothing here throws; a failed call becomes <see cref="WindowOperationResult.Failed"/> with the Win32 error code.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32WindowOperations : IWindowOperations
{
    private readonly IWin32Windows _query;
    private readonly IWin32WindowControl _control;

    public Win32WindowOperations()
        : this(new Win32Windows())
    {
    }

    private Win32WindowOperations(Win32Windows win)
        : this(win, win)
    {
    }

    internal Win32WindowOperations(IWin32Windows query, IWin32WindowControl control)
    {
        _query = query;
        _control = control;
    }

    public HostPlatform Platform => HostPlatform.Windows;

    public bool Supports(WindowOperation operation) => Enum.IsDefined(operation);

    public WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null)
    {
        var hwnd = window.RootHandle;
        if (!_query.IsWindow(hwnd))
        {
            return WindowOperationResult.Failed("window gone");
        }

        return operation switch
        {
            WindowOperation.Close => Close(hwnd),
            WindowOperation.Minimize => Show(hwnd, NativeMethods.SwMinimize),
            WindowOperation.MaximizeOrRestore => Show(hwnd, _query.IsZoomed(hwnd) ? NativeMethods.SwRestore : NativeMethods.SwMaximize),
            WindowOperation.ToggleAlwaysOnTop => SetTopmost(hwnd, !_query.IsTopmost(hwnd)),
            WindowOperation.Center => Place(hwnd, WindowGeometry.Center),
            WindowOperation.SetSize => SetSize(hwnd, size),
            WindowOperation.SnapLeftHalf => Place(hwnd, (_, work) => WindowGeometry.LeftHalf(work)),
            WindowOperation.SnapRightHalf => Place(hwnd, (_, work) => WindowGeometry.RightHalf(work)),
            _ => WindowOperationResult.NotSupported(operation, Platform),
        };
    }

    private WindowOperationResult Close(nint hwnd)
        => _control.PostSysCommandClose(hwnd) ? WindowOperationResult.Ok : Failed("PostMessage(SC_CLOSE)");

    private WindowOperationResult Show(nint hwnd, int command)
    {
        _control.ShowWindow(hwnd, command);
        return WindowOperationResult.Ok;
    }

    private WindowOperationResult SetTopmost(nint hwnd, bool topmost)
        => _control.SetTopmost(hwnd, topmost) ? WindowOperationResult.Ok : Failed("SetWindowPos(topmost)");

    private WindowOperationResult SetSize(nint hwnd, WindowSize? size)
    {
        if (size is null)
        {
            return WindowOperationResult.Failed("no size");
        }

        if (size.Value.Width <= 0 || size.Value.Height <= 0)
        {
            return WindowOperationResult.Failed($"size must be positive, got {size.Value.Width}×{size.Value.Height}");
        }

        return Place(hwnd, (window, work) => WindowGeometry.Resize(window, size.Value.Width, size.Value.Height, work));
    }

    /// <summary>Restore first when maximized or minimized, so the rectangle read next is the normal one.</summary>
    private WindowOperationResult Place(nint hwnd, Func<ScreenRect, ScreenRect, ScreenRect> bounds)
    {
        if (_query.IsZoomed(hwnd) || _query.IsIconic(hwnd))
        {
            _control.ShowWindow(hwnd, NativeMethods.SwRestore);
        }

        if (!_query.TryWorkArea(hwnd, out var work))
        {
            return WindowOperationResult.Failed("no monitor for the window");
        }

        if (!_query.TryWindowRect(hwnd, out var window))
        {
            return WindowOperationResult.Failed("GetWindowRect failed");
        }

        var target = bounds(window, work);
        return _control.SetWindowBounds(hwnd, target.Left, target.Top, WindowGeometry.Width(target), WindowGeometry.Height(target))
            ? WindowOperationResult.Ok
            : Failed("SetWindowPos");
    }

    private WindowOperationResult Failed(string call) => WindowOperationResult.Failed($"{call} failed ({_control.LastError()})");
}
