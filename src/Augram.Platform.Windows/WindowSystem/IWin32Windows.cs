namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// Read-only Win32 window queries, one method per native call, so <see cref="WindowIdentityReader"/>,
/// <see cref="Win32WindowOperations"/> and the pure rules can be tested with a fake. Side effects live in
/// <see cref="IWin32Foreground"/> and <see cref="IWin32WindowControl"/>. <see cref="Interop.Win32Windows"/> is
/// the only real implementation.
/// </summary>
internal interface IWin32Windows
{
    nint WindowFromPoint(int x, int y);

    nint Parent(nint hwnd);

    nint Root(nint hwnd);

    nint RootOwner(nint hwnd);

    string ClassName(nint hwnd);

    string Title(nint hwnd);

    /// <summary>Owning thread id and process id; both zero for an invalid handle.</summary>
    (uint ThreadId, int ProcessId) ThreadAndProcess(nint hwnd);

    /// <summary>Full image path via limited-rights query, or null when access is denied.</summary>
    string? ProcessImagePath(int processId);

    /// <summary>Process base name from the BCL when the image path is unreadable; null when the process is gone.</summary>
    string? ProcessBaseName(int processId);

    /// <summary>First visible direct child with this class, or zero.</summary>
    nint FindVisibleChild(nint parent, string className);

    nint ShellWindow();

    nint DesktopWindow();

    bool TryWindowRect(nint hwnd, out ScreenRect rect);

    bool TryMonitorRect(nint hwnd, out ScreenRect rect);

    /// <summary>The work area (monitor minus taskbar) of the monitor nearest the window; false when there is none.</summary>
    bool TryWorkArea(nint hwnd, out ScreenRect rect);

    /// <summary>False once the handle no longer names a window (closed since the stroke began).</summary>
    bool IsWindow(nint hwnd);

    bool IsIconic(nint hwnd);

    bool IsZoomed(nint hwnd);

    /// <summary><c>WS_EX_TOPMOST</c> is set in the window's extended style.</summary>
    bool IsTopmost(nint hwnd);
}
