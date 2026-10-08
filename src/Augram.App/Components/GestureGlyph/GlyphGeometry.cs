using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Media;

namespace Augram.App.Components.GestureGlyph;

/// <summary>
/// The F4 glyph recipe, as pure functions so the maths is testable without a control: normalise a
/// raw point list by its bounding box, scale it on the longer axis into the tile (aspect ratio
/// preserved, centred, inside the padding), draw the polyline, and finish with an arrowhead on the
/// last segment so direction is visible (up-flick is not down-flick, CLAUDE.md invariant 3).
/// </summary>
public static class GlyphGeometry
{
    private const double ArrowAngleDegrees = 30;
    private const double Epsilon = 1e-6;

    /// <summary>Points mapped into <paramref name="tile"/> minus <paramref name="padding"/>, longer axis filling it, aspect ratio kept, centred.</summary>
    public static Point[] Normalise(IReadOnlyList<GesturePoint> points, Size tile, Thickness padding)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return [];
        }

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        var innerWidth = Math.Max(0, tile.Width - padding.Left - padding.Right);
        var innerHeight = Math.Max(0, tile.Height - padding.Top - padding.Bottom);
        var width = maxX - minX;
        var height = maxY - minY;
        var scale = Math.Min(
            width > Epsilon ? innerWidth / width : double.PositiveInfinity,
            height > Epsilon ? innerHeight / height : double.PositiveInfinity);
        if (double.IsInfinity(scale))
        {
            scale = 0;
        }

        var offsetX = padding.Left + ((innerWidth - (width * scale)) / 2) - (minX * scale);
        var offsetY = padding.Top + ((innerHeight - (height * scale)) / 2) - (minY * scale);
        var result = new Point[points.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Point((points[i].X * scale) + offsetX, (points[i].Y * scale) + offsetY);
        }

        return result;
    }

    /// <summary>Normalised polyline plus arrowhead, for a tile.</summary>
    public static Geometry Build(IReadOnlyList<GesturePoint> points, Size tile, Thickness padding, double arrowLength)
        => FromPoints(Normalise(points, tile, padding), arrowLength);

    /// <summary>Polyline plus arrowhead in the points' own coordinates, for a draw area showing the stroke where it was drawn.</summary>
    public static Geometry BuildRaw(IReadOnlyList<GesturePoint> points, double arrowLength)
    {
        ArgumentNullException.ThrowIfNull(points);
        return FromPoints(points.Select(point => new Point(point.X, point.Y)).ToArray(), arrowLength);
    }

    /// <summary>Normalised points with repeats dropped: what <see cref="GlyphStroke"/> draws, segment by segment.</summary>
    public static Point[] StrokePoints(IReadOnlyList<GesturePoint> points, Size tile, Thickness padding)
        => [.. Distinct(Normalise(points, tile, padding))];

    /// <summary>
    /// How far along the stroke each point lies, 0 at the start to 1 at the end, by arc length: the gradient's position
    /// (Joel, 2026-10-08), so a slow, dense part of the stroke does not hog the colour range. All zeros for a single point.
    /// </summary>
    public static double[] Progress(IReadOnlyList<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var progress = new double[points.Count];
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            total += Math.Sqrt((dx * dx) + (dy * dy));
            progress[i] = total;
        }

        if (total > Epsilon)
        {
            for (var i = 1; i < progress.Length; i++)
            {
                progress[i] /= total;
            }
        }

        return progress;
    }

    /// <summary>The two wing points of an arrowhead at <paramref name="tip"/>, swept back along the direction from <paramref name="from"/>.</summary>
    public static (Point Left, Point Right) ArrowWings(Point from, Point tip, double length)
    {
        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance < Epsilon)
        {
            return (tip, tip);
        }

        var back = Math.Atan2(dy, dx) + Math.PI;
        var angle = ArrowAngleDegrees * Math.PI / 180;
        return (
            new Point(tip.X + (length * Math.Cos(back - angle)), tip.Y + (length * Math.Sin(back - angle))),
            new Point(tip.X + (length * Math.Cos(back + angle)), tip.Y + (length * Math.Sin(back + angle))));
    }

    private static Geometry FromPoints(Point[] points, double arrowLength)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var distinct = Distinct(points);
        if (distinct.Count == 0)
        {
            return geometry;
        }

        context.BeginFigure(distinct[0], isFilled: false);
        for (var i = 1; i < distinct.Count; i++)
        {
            context.LineTo(distinct[i]);
        }

        if (distinct.Count == 1)
        {
            // A dot: a zero-length segment with round caps renders as a point.
            context.LineTo(distinct[0]);
        }

        context.EndFigure(isClosed: false);

        if (distinct.Count >= 2)
        {
            var tip = distinct[^1];
            var (left, right) = ArrowWings(distinct[^2], tip, arrowLength);
            context.BeginFigure(left, isFilled: false);
            context.LineTo(tip);
            context.LineTo(right);
            context.EndFigure(isClosed: false);
        }

        return geometry;
    }

    private static List<Point> Distinct(IReadOnlyList<Point> points)
    {
        var result = new List<Point>(points.Count);
        foreach (var point in points)
        {
            if (result.Count == 0 || Math.Abs(point.X - result[^1].X) > Epsilon || Math.Abs(point.Y - result[^1].Y) > Epsilon)
            {
                result.Add(point);
            }
        }

        return result;
    }
}
