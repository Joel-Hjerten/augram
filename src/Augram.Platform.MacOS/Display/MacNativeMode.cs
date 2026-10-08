namespace Augram.Platform.MacOS.Display;

/// <summary>
/// One <c>CGDisplayMode</c> as plain values: the size in points ("looks like") and in pixels (twice the points for a
/// HiDPI mode), the refresh rate as CoreGraphics reports it (0 when the display does not say), whether the desktop can
/// run in it, and its IOKit mode id, which tells the mode apart from its twins when it is applied.
/// </summary>
internal sealed record MacNativeMode(int Width, int Height, int PixelWidth, int PixelHeight, double RefreshHz, bool Usable, int IoModeId)
{
    public bool IsHiDpi => PixelWidth > Width;
}
