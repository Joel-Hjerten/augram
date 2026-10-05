namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// UWP apps run inside <c>ApplicationFrameHost.exe</c>: the frame (<c>ApplicationFrameWindow</c>) belongs to the host,
/// the real app owns a <c>Windows.UI.Core.CoreWindow</c> child. A point on the title bar or border hits the host, a
/// point inside hits the CoreWindow. Identity must name the real app either way, so the process is read from the
/// CoreWindow whenever the root is a frame window (GestureSign and SP.net do the same, reference §9 [8264]).
/// </summary>
internal static class UwpHostRule
{
    public const string FrameClass = "ApplicationFrameWindow";
    public const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    /// <summary>The window whose process identifies the app: the CoreWindow child for UWP frames, else the handle itself.</summary>
    public static nint ProcessWindow(IWin32Windows win, nint handle, nint root, string rootClass)
    {
        if (!string.Equals(rootClass, FrameClass, StringComparison.Ordinal))
        {
            return handle;
        }

        var core = win.FindVisibleChild(root, CoreWindowClass);
        return core == 0 ? handle : core;
    }
}
