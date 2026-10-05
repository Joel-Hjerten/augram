namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// A21: a window is full screen when its rectangle equals its monitor's rectangle exactly and it is not the
/// desktop (whose <c>Progman</c>/<c>WorkerW</c> root always covers the monitor). Borderless games pass this;
/// maximized windows do not, because their frame overlaps the work area, not the monitor edges.
/// </summary>
internal static class FullScreenRule
{
    public static bool IsFullScreen(ScreenRect window, ScreenRect monitor, string rootClass)
        => window == monitor && !DesktopRule.IsDesktopClass(rootClass);
}
