namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// The rules that turn the window server's front-to-back list into a <c>WindowIdentity</c>'s answers, without asking
/// any app: which window a point hits, whether it is the desktop, whether it is full screen (A21).
/// </summary>
internal static class MacWindowPick
{
    public const int NormalLayer = 0;

    /// <summary>
    /// The frontmost window containing the point, skipping fully transparent windows, this process's windows above the
    /// normal layer (the trail overlay; Augram's own settings window stays a target, as on Windows) and display-sized
    /// windows above the normal layer: the Dock draws itself in a transparent window as large as its display (2026-10-07,
    /// every stroke on that display resolved to Dock), and screen-overlay utilities do the same; the window server's list
    /// cannot say which of their pixels take clicks. A smaller window on a higher layer (the menu bar, a status item)
    /// wins over the normal window behind it, so a gesture there never acts on a window the user was not pointing at.
    /// </summary>
    public static MacWindowInfo? At(IEnumerable<MacWindowInfo> frontToBack, double x, double y, int ownProcessId, IReadOnlyList<MacRect> displays)
    {
        ArgumentNullException.ThrowIfNull(frontToBack);
        ArgumentNullException.ThrowIfNull(displays);
        return frontToBack.FirstOrDefault(window =>
            !(window.Layer > NormalLayer && (window.ProcessId == ownProcessId || CoversADisplay(window, displays)))
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
        return window.Layer == NormalLayer && CoversADisplay(window, displays);
    }

    private static bool CoversADisplay(MacWindowInfo window, IReadOnlyList<MacRect> displays)
        => displays.Any(display => display.IsNear(window.Bounds, 1));
}
