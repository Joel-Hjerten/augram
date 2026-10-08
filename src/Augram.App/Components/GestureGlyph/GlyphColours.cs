using Avalonia.Media;

namespace Augram.App.Components.GestureGlyph;

/// <summary>
/// The start colour of a gesture picture's gradient, derived from its end colour (the trail colour from Options; Joel,
/// 2026-10-08): darker, a little more saturated, and hue-shifted the way illustrators shade, towards blue-violet along the
/// shorter way round the colour wheel. Yellow starts amber (it goes round through orange), green a darker, faintly teal
/// green, cyan a deeper blue, red a crimson. Greys only darken: the shift scales with saturation, so a hueless colour
/// keeps its hue. Hue is HSV's (0–360°): in it pure yellow is nearer blue-violet by way of red, which is the warm way Joel
/// asked for; a perceptual space puts yellow on the green side and would shade it olive.
/// </summary>
public static class GlyphColours
{
    /// <summary>Where shading pulls hues: blue-violet.</summary>
    public const double ShadowHue = 250;

    /// <summary>The largest hue rotation, in degrees.</summary>
    public const double MaxHueShift = 20;

    /// <summary>The start's brightness as a share of the end's.</summary>
    public const double Darken = 0.7;

    /// <summary>Added saturation, so the dark end does not look muddy.</summary>
    public const double SaturationBoost = 0.1;

    public static Color StartFor(Color end)
    {
        var (hue, saturation, value) = ToHsv(end);
        var towards = SignedDistance(hue, ShadowHue);
        var shift = Math.Clamp(towards, -MaxHueShift, MaxHueShift) * saturation;
        var start = FromHsv(Wrap(hue + shift), saturation > 0 ? Math.Min(1, saturation + SaturationBoost) : 0, value * Darken);
        return Color.FromArgb(end.A, start.R, start.G, start.B);
    }

    /// <summary>The hue (0–360), saturation and value (0–1) of <paramref name="color"/>.</summary>
    public static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        double hue = 0;
        if (delta > 0)
        {
            hue = max == r ? 60 * (((g - b) / delta) % 6)
                : max == g ? 60 * (((b - r) / delta) + 2)
                : 60 * (((r - g) / delta) + 4);
        }

        return (Wrap(hue), max > 0 ? delta / max : 0, max);
    }

    public static Color FromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var sector = Wrap(hue) / 60;
        var x = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = value - chroma;
        return Color.FromRgb(Channel(r + m), Channel(g + m), Channel(b + m));
    }

    /// <summary>From <paramref name="from"/> to <paramref name="to"/> the shorter way round, −180 to 180; a tie goes down (through red for yellow).</summary>
    private static double SignedDistance(double from, double to)
    {
        var distance = Wrap(to - from);
        return distance >= 180 ? distance - 360 : distance;
    }

    private static double Wrap(double degrees) => ((degrees % 360) + 360) % 360;

    private static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
