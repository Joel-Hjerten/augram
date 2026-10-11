using Augram.App.Components.GestureGlyph;
using Avalonia.Media;

namespace Augram.App.Themes;

/// <summary>
/// The accent's shades for one theme variant, derived from one colour (plan 0006 decision 6), and the gesture pictures'
/// colours from the trail colour. Same maths as the mockup (design/mockups/augram-glass-theme.html, <c>themeVars</c>):
/// <list type="bullet">
/// <item>Dark: fills at least 3:1 against the panel, accent-coloured text at least 5:1.</item>
/// <item>Light: fills stay bright, darkened only to 1.6:1 against white and given a deeper edge; only accent-coloured text
/// takes the dark shade (4.5:1). Darkening every fill to 3:1 turned yellow mustard (Joel, 2026-10-11).</item>
/// <item>Text on a fill is whichever of near-black or white contrasts more, so yellow gets dark text.</item>
/// <item>Pictures: the trail colour, darkened only as far as a thin line needs (3:1 on dark tiles, 2:1 on light ones);
/// their start is <see cref="GlyphColours.StartFor"/> of that.</item>
/// </list>
/// Shades move along HSL lightness in 2 % steps until the contrast is reached.
/// </summary>
public sealed record AccentPalette(Color Fill, Color OnFill, Color Soft, Color Line, Color Edge, Color Text, Color GlyphEnd, Color GlyphStart)
{
    private static readonly Color NearBlack = Color.FromRgb(23, 20, 10);
    private static readonly Color DarkPanel = Color.FromRgb(42, 41, 47);
    private static readonly Color DarkTile = Color.FromRgb(46, 45, 51);
    private static readonly Color LightTile = Color.FromRgb(244, 244, 247);

    public static AccentPalette For(bool dark, Color accent, Color trail)
    {
        var background = dark ? DarkPanel : Colors.White;
        var step = dark ? 0.02 : -0.02;
        var fill = Shift(accent, c => Contrast(c, background) >= (dark ? 3 : 1.6), step);
        var mid = Shift(accent, c => Contrast(c, background) >= (dark ? 3 : 2.4), step);
        var text = Shift(accent, c => Contrast(c, background) >= (dark ? 5 : 4.5), step);
        var glyphEnd = Shift(trail, c => Contrast(c, dark ? DarkTile : LightTile) >= (dark ? 3 : 2), step);
        var onFill = Contrast(fill, NearBlack) >= Contrast(fill, Colors.White) ? NearBlack : Colors.White;
        return new AccentPalette(
            fill,
            onFill,
            WithAlpha(fill, dark ? 0.16 : 0.24),
            WithAlpha(mid, dark ? 0.48 : 0.6),
            dark ? Colors.Transparent : WithAlpha(mid, 0.6),
            text,
            glyphEnd,
            GlyphColours.StartFor(glyphEnd));
    }

    /// <summary>The WCAG contrast ratio of two opaque colours, 1 to 21.</summary>
    public static double Contrast(Color a, Color b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static Color Shift(Color colour, Func<Color, bool> good, double step)
    {
        var (hue, saturation, lightness) = ToHsl(colour);
        var current = colour;
        for (var i = 0; i < 60 && !good(current); i++)
        {
            lightness = Math.Clamp(lightness + step, 0, 1);
            current = FromHsl(hue, saturation, lightness);
        }

        return current;
    }

    private static Color WithAlpha(Color colour, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), colour.R, colour.G, colour.B);

    private static double Luminance(Color colour)
    {
        static double Linear(byte channel)
        {
            var v = channel / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Color colour)
    {
        var r = colour.R / 255.0;
        var g = colour.G / 255.0;
        var b = colour.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2;
        if (max == min)
        {
            return (0, 0, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        var hue = max == r ? ((g - b) / delta) + (g < b ? 6 : 0)
            : max == g ? ((b - r) / delta) + 2
            : ((r - g) / delta) + 4;
        return (hue * 60, saturation, lightness);
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        if (saturation == 0)
        {
            var grey = Channel(lightness);
            return Color.FromRgb(grey, grey, grey);
        }

        var h = ((hue % 360) + 360) % 360 / 360;
        var q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - (lightness * saturation);
        var p = (2 * lightness) - q;

        double Component(double t)
        {
            t = (t + 1) % 1;
            return t < 1.0 / 6 ? p + ((q - p) * 6 * t)
                : t < 0.5 ? q
                : t < 2.0 / 3 ? p + ((q - p) * ((2.0 / 3) - t) * 6)
                : p;
        }

        return Color.FromRgb(Channel(Component(h + (1.0 / 3))), Channel(Component(h)), Channel(Component(h - (1.0 / 3))));
    }

    private static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
