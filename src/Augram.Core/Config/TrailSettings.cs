namespace Augram.Core.Config;

/// <summary>
/// Trail overlay look (requirements F6). Defaults are the StrokesPlus.net values the
/// requirements name: 5 px, 50 % opacity, green 0/255/64. Width is in device-independent
/// pixels; the overlay scales it by the DPI of the monitor the stroke starts on.
/// </summary>
public sealed record TrailSettings
{
    public double WidthPx { get; init; } = 5;

    /// <summary>0 (invisible) to 1 (opaque).</summary>
    public double Opacity { get; init; } = 0.5;

    public RgbColor Colour { get; init; } = new(0, 255, 64);

    public static TrailSettings Default { get; } = new();
}
