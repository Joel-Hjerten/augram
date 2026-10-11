namespace Augram.Core.Config;

/// <summary>
/// Trail overlay look (requirements F6). Width and opacity default to the StrokesPlus.net
/// values the requirements name: 5 px, 50 %. The colour defaults to Augram's yellow
/// <c>#F5C542</c> (plan 0006 decision 7; it was StrokesPlus.net's green 0/255/64), and a
/// config that has a colour keeps it. Width is in device-independent pixels; the overlay
/// scales it by the DPI of the monitor the stroke starts on.
/// </summary>
public sealed record TrailSettings
{
    public double WidthPx { get; init; } = 5;

    /// <summary>0 (invisible) to 1 (opaque).</summary>
    public double Opacity { get; init; } = 0.5;

    public RgbColor Colour { get; init; } = new(245, 197, 66);

    public static TrailSettings Default { get; } = new();
}
