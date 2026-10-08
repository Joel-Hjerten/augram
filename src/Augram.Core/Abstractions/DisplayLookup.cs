namespace Augram.Core.Abstractions;

/// <summary>
/// Finds the display a Display step acts on among <see cref="IDisplayModes.Displays"/>: the one whose bounds contain the
/// gesture start (both are in the hook's coordinates), or the main one. Pure, so the rule is the same on every platform.
/// </summary>
public static class DisplayLookup
{
    /// <summary>The display for <paramref name="target"/>; null when no display contains the point or none is listed.</summary>
    public static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, DisplayTarget target, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(displays);
        return target == DisplayTarget.Main ? Main(displays) : displays.FirstOrDefault(display => display.Bounds.Contains(x, y));
    }

    /// <summary>The display flagged main, else the one at the desktop origin, else the first listed.</summary>
    public static DisplayInfo? Main(IReadOnlyList<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        return displays.FirstOrDefault(display => display.IsMain)
            ?? displays.FirstOrDefault(display => display.Bounds.Contains(0, 0))
            ?? displays.FirstOrDefault();
    }
}
