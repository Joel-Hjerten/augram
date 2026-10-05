using System.Globalization;

namespace Augram.Core.Config;

/// <summary>An opaque sRGB colour; opacity is a separate setting. Core has no System.Drawing. Text form is <c>#RRGGBB</c>.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Accepts <c>#RRGGBB</c> or <c>RRGGBB</c>, any case.</summary>
    public static bool TryParse(string? text, out RgbColor colour)
    {
        colour = default;
        var hex = text?.Trim().TrimStart('#');
        if (hex is null || hex.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        colour = new RgbColor(r, g, b);
        return true;
    }
}
