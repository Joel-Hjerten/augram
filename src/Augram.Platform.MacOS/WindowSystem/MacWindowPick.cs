namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The rules that turn the window server's front-to-back list into a <c>WindowIdentity</c>'s answers, without asking
/// any app: which window a point hits, whether it is the desktop, whether it is full screen (A21).
/// </summary>
internal static class MacWindowPick
{
    public const int NormalLayer = 0;

    /// <summary>
    /// The frontmost window containing the point, skipping fully transparent windows and this process's windows above the
    /// normal layer (the trail overlay covers the screen during a stroke; Augram's own settings window stays a target, as
    /// on Windows). A window on a higher layer (the Dock, the menu bar) wins over the normal window behind it, so a
    /// gesture there never acts on a window the user was not pointing at.
    /// </summary>
    public static MacWindowInfo? At(IEnumerable<MacWindowInfo> frontToBack, double x, double y, int ownProcessId)
    {
        ArgumentNullException.ThrowIfNull(frontToBack);
        return frontToBack.FirstOrDefault(window =>
            !(window.ProcessId == ownProcessId && window.Layer > NormalLayer)
            && window.Alpha > 0
            && !window.Bounds.IsEmpty
            && window.Bounds.Contains(x, y));
    }

    /// <summary>The wallpaper and Finder's desktop icons live below the normal layer; A20 never activates them.</summary>
    public static bool IsDesktop(MacWindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.Layer < NormalLayer;
    }

    /// <summary>A21: a normal-layer window that covers a whole display, as native full-screen windows and games do.</summary>
    public static bool IsFullScreen(MacWindowInfo window, IReadOnlyList<MacRect> displays)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(displays);
        return window.Layer == NormalLayer && displays.Any(display => display.IsNear(window.Bounds, 1));
    }
}
