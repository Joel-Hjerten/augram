namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The arithmetic behind <see cref="Win32WindowOperations"/>'s placement operations, over <see cref="ScreenRect"/>
/// so it is tested without a window. Every input and output is an outer rectangle in physical pixels; the work
/// area is the monitor minus the taskbar. A window larger than the work area cannot fit, so these keep its
/// top-left edge inside and let the far edge overhang, which is what keeps the title bar reachable.
/// </summary>
internal static class WindowGeometry
{
    /// <summary>The window moved to the centre of the work area, size kept.</summary>
    public static ScreenRect Center(ScreenRect window, ScreenRect workArea)
    {
        var width = Width(window);
        var height = Height(window);
        var left = workArea.Left + Math.Max(0, (Width(workArea) - width) / 2);
        var top = workArea.Top + Math.Max(0, (Height(workArea) - height) / 2);
        return new ScreenRect(left, top, left + width, top + height);
    }

    /// <summary>
    /// The window resized to <paramref name="width"/> × <paramref name="height"/> from its top-left corner. When the
    /// new right or bottom edge would leave the work area the window is shifted back inside, but never past the
    /// work area's own left or top edge.
    /// </summary>
    public static ScreenRect Resize(ScreenRect window, int width, int height, ScreenRect workArea)
    {
        var left = Fit(window.Left, width, workArea.Left, workArea.Right);
        var top = Fit(window.Top, height, workArea.Top, workArea.Bottom);
        return new ScreenRect(left, top, left + width, top + height);
    }

    /// <summary>The left half of the work area; an odd width gives the left half the smaller share.</summary>
    public static ScreenRect LeftHalf(ScreenRect workArea)
        => new(workArea.Left, workArea.Top, workArea.Left + Width(workArea) / 2, workArea.Bottom);

    /// <summary>The right half of the work area; an odd width gives the right half the extra pixel.</summary>
    public static ScreenRect RightHalf(ScreenRect workArea)
        => new(workArea.Left + Width(workArea) / 2, workArea.Top, workArea.Right, workArea.Bottom);

    public static int Width(ScreenRect rect) => rect.Right - rect.Left;

    public static int Height(ScreenRect rect) => rect.Bottom - rect.Top;

    private static int Fit(int start, int length, int areaStart, int areaEnd)
        => start + length > areaEnd ? Math.Max(areaStart, areaEnd - length) : start;
}
