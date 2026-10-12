namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The one "a zero handle means none, fall back" rule of this folder: <c>GetAncestor</c> answers zero for a handle it cannot
/// walk, and every caller then keeps the window it asked about (or the root it already has).
/// </summary>
internal static class WindowHandles
{
    /// <summary><paramref name="value"/>, or <paramref name="fallback"/> when it is zero (no such window).</summary>
    public static nint Or(nint value, nint fallback) => value == 0 ? fallback : value;

    /// <summary>The root owner of <paramref name="hwnd"/> (<c>GA_ROOTOWNER</c>), or the window itself when there is none; zero for zero, without asking.</summary>
    public static nint RootOwnerOrSelf(this IWin32Windows windows, nint hwnd) => hwnd == 0 ? 0 : Or(windows.RootOwner(hwnd), hwnd);

    /// <summary>The top-level window of <paramref name="hwnd"/> (<c>GA_ROOT</c>: an owned dialog is its own), or the window itself; zero for zero, without asking.</summary>
    public static nint RootOrSelf(this IWin32Windows windows, nint hwnd) => hwnd == 0 ? 0 : Or(windows.Root(hwnd), hwnd);
}
