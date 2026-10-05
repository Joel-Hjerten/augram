using Augram.Core.Gestures;

namespace Augram.Core.Tests.Fixtures;

/// <summary>Synthetic strokes for recognizer tests. Screen coordinates: Y grows downwards.</summary>
internal static class StrokeBuilder
{
    public static GesturePoint P(double x, double y) => new(x, y);

    /// <summary>Straight line with <paramref name="pointCount"/> evenly spaced raw points.</summary>
    public static GesturePoint[] Line(GesturePoint from, GesturePoint to, int pointCount = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pointCount, 2);

        var points = new GesturePoint[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            double t = (double)i / (pointCount - 1);
            points[i] = new GesturePoint(from.X + (t * (to.X - from.X)), from.Y + (t * (to.Y - from.Y)));
        }

        return points;
    }

    /// <summary>Corner-to-corner polyline, each leg carrying <paramref name="pointsPerLeg"/> points.</summary>
    public static GesturePoint[] Polyline(int pointsPerLeg, params GesturePoint[] corners)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(corners.Length, 2);

        var points = new List<GesturePoint> { corners[0] };
        for (int i = 1; i < corners.Length; i++)
        {
            points.AddRange(Line(corners[i - 1], corners[i], pointsPerLeg).Skip(1));
        }

        return points.ToArray();
    }

    /// <summary>Circular arc, angles in radians, screen orientation (clockwise when the end angle is larger).</summary>
    public static GesturePoint[] Arc(GesturePoint centre, double radius, double startAngle, double endAngle, int pointCount)
    {
        var points = new GesturePoint[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            double angle = startAngle + ((endAngle - startAngle) * i / (pointCount - 1));
            points[i] = new GesturePoint(centre.X + (radius * Math.Cos(angle)), centre.Y + (radius * Math.Sin(angle)));
        }

        return points;
    }

    public static GesturePoint[] Scale(IReadOnlyList<GesturePoint> points, double factor)
        => points.Select(p => new GesturePoint(p.X * factor, p.Y * factor)).ToArray();

    public static GesturePoint[] Translate(IReadOnlyList<GesturePoint> points, double dx, double dy)
        => points.Select(p => new GesturePoint(p.X + dx, p.Y + dy)).ToArray();

    public static GesturePoint[] Mirror(IReadOnlyList<GesturePoint> points)
        => points.Select(p => new GesturePoint(-p.X, p.Y)).ToArray();

    /// <summary>Deterministic jitter: every coordinate shifted uniformly within ±<paramref name="amplitude"/>.</summary>
    public static GesturePoint[] Jitter(IReadOnlyList<GesturePoint> points, double amplitude, int seed)
    {
        var random = new Random(seed);
        return points
            .Select(p => new GesturePoint(p.X + Offset(random, amplitude), p.Y + Offset(random, amplitude)))
            .ToArray();
    }

    private static double Offset(Random random, double amplitude) => ((random.NextDouble() * 2) - 1) * amplitude;
}
