using Augram.App.Components.GestureGlyph;
using Augram.Core.Gestures;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// One column of a <see cref="ListSpec"/>: header text, how to read a cell, and a width (0 = share the
/// rest). A text cell by default; <see cref="Component"/> supplies a control per cell instead, as
/// <see cref="Glyph"/> does for a gesture thumbnail (the same <see cref="GestureGlyph"/> the Gestures
/// tab uses, sized by the theme's <c>row-glyph</c> class).
/// </summary>
public sealed record ListColumn(string Title, Func<object, string> Cell, double Width = 0, Func<object, Control>? Component = null)
{
    public const double GlyphWidth = 56;

    /// <summary>A column that draws the points <paramref name="points"/> yields for the row, or nothing when it yields null.</summary>
    public static ListColumn Glyph(string title, Func<object, IReadOnlyList<GesturePoint>?> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return new ListColumn(title, _ => string.Empty, GlyphWidth, row =>
        {
            var glyph = new GestureGlyph { Points = points(row) };
            glyph.Classes.Add("row-glyph");
            return glyph;
        });
    }
}
