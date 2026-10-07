namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The Win32 calls <see cref="Win32WindowOperations"/> needs that change a window, separated from the
/// <see cref="IWin32Windows"/> queries because they have side effects (the same split as <see cref="IWin32Foreground"/>).
/// Each is one native call; a false return means the call failed and <see cref="LastError"/> says why.
/// </summary>
internal interface IWin32WindowControl
{
    /// <summary><c>ShowWindow</c> with <c>SW_MINIMIZE</c>, <c>SW_MAXIMIZE</c> or <c>SW_RESTORE</c>. Reports no error (its return is "was visible").</summary>
    void ShowWindow(nint hwnd, int command);

    /// <summary>Posts <c>WM_SYSCOMMAND</c> / <c>SC_CLOSE</c>, what the window's own close button sends; never kills the process.</summary>
    bool PostSysCommandClose(nint hwnd);

    /// <summary><c>SetWindowPos</c> to <c>HWND_TOPMOST</c> or <c>HWND_NOTOPMOST</c> without moving, sizing or activating.</summary>
    bool SetTopmost(nint hwnd, bool topmost);

    /// <summary><c>SetWindowPos</c> to the outer rectangle in physical pixels, keeping z-order and not activating.</summary>
    bool SetWindowBounds(nint hwnd, int x, int y, int width, int height);

    /// <summary>The Win32 error of the last failed call on this thread, for the result's reason.</summary>
    int LastError();
}
