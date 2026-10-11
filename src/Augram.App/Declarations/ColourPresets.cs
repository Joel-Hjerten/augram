using Augram.Core.Config;

namespace Augram.App.Declarations;

/// <summary>
/// The swatch rows colour fields offer (plan 0006 decision 8). Data, not theme: a preset is a value the user picks, the
/// same in every theme, so it lives here rather than in <c>Tokens.axaml</c>.
/// </summary>
public static class ColourPresets
{
    /// <summary>
    /// The trail's and the accent's swatches, in rainbow order (Joel, 2026-10-11). Yellow is the fresh-install trail colour
    /// (decision 7); Green is StrokesPlus.net's 0/255/64, so an imported or older config matches a swatch.
    /// </summary>
    public static IReadOnlyList<ColourPreset> Rainbow { get; } =
    [
        new("Red", new RgbColor(0xFF, 0x5A, 0x4F)),
        new("Orange", new RgbColor(0xFF, 0x8A, 0x3D)),
        new("Yellow", new RgbColor(0xF5, 0xC5, 0x42)),
        new("Green", new RgbColor(0x00, 0xFF, 0x40)),
        new("Cyan", new RgbColor(0x36, 0xD6, 0xE7)),
        new("Blue", new RgbColor(0x5B, 0x9B, 0xFF)),
        new("Purple", new RgbColor(0xA7, 0x8B, 0xFA)),
    ];
}
