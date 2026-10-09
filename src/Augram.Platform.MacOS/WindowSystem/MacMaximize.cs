namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// What <c>MaximizeOrRestore</c> means on macOS (D6, working choice 2026-10-07): fill the visible frame of the window's
/// screen, as Windows fills the work area, and restore the frame from before. The alternatives were weighed and left:
/// zoom (the green button's Option-click) is defined by each app and often fits the content rather than the screen,
/// and native full screen moves the window to its own Space.
/// </summary>
internal static class MacMaximize
{
    /// <summary>
    /// Apps round a size to their own grid (Terminal to whole character cells), so a filled window ends up within a few
    /// points of the visible frame rather than exactly on it.
    /// </summary>
    public const double FillTolerance = 24;

    public static bool Fills(MacRect window, MacRect visible) => window.IsNear(visible, FillTolerance);

    /// <summary>The share of the visible frame <see cref="DefaultRestore"/> gives a window, each way.</summary>
    public const double DefaultRestoreShare = 2.0 / 3.0;

    /// <summary>
    /// Where a filled window goes when Augram did not fill it and remembers no frame (2026-10-09: macOS's own zoom or
    /// tiling filled it): centred on the visible frame at two thirds of its width and height. Predictable, and never what
    /// pressing the green button did on current macOS, which is native full screen.
    /// </summary>
    public static MacRect DefaultRestore(MacRect visible)
    {
        var width = Math.Round(visible.Width * DefaultRestoreShare);
        var height = Math.Round(visible.Height * DefaultRestoreShare);
        return new MacRect(Math.Round(visible.X + ((visible.Width - width) / 2)), Math.Round(visible.Y + ((visible.Height - height) / 2)), width, height);
    }

    /// <summary>The index of the screen a window belongs to: the one containing its centre, else the one it overlaps most, else the first.</summary>
    public static int ScreenFor(MacRect window, IReadOnlyList<MacRect> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        for (var i = 0; i < screens.Count; i++)
        {
            if (screens[i].Contains(window.CenterX, window.CenterY))
            {
                return i;
            }
        }

        var best = 0;
        var bestArea = 0.0;
        for (var i = 0; i < screens.Count; i++)
        {
            var area = screens[i].IntersectionArea(window);
            if (area > bestArea)
            {
                best = i;
                bestArea = area;
            }
        }

        return best;
    }
}
