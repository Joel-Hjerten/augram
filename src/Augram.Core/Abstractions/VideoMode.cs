namespace Augram.Core.Abstractions;

/// <summary>
/// One mode a display offers or runs at: a resolution and a refresh rate (Windows calls it a graphics mode, macOS a
/// display mode). Modes that differ only in what the platform keeps beside them (Windows' fixed-output variants,
/// macOS's HiDPI and 1x twins) are one mode here; the adapter picks among its variants when it applies one. Shown as
/// "1920×1080 at 119.88 Hz".
/// </summary>
public readonly record struct VideoMode(DisplayResolution Resolution, RefreshRate Refresh)
{
    public override string ToString() => Refresh.IsKnown ? $"{Resolution} at {Refresh}" : Resolution.ToString();
}
