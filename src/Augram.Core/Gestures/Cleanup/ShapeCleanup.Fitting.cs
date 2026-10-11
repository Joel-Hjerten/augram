namespace Augram.Core.Gestures.Cleanup;

/// <summary>Shape cleanup, the fitting: a piece as a line, an arc or smoothed points, the least-squares circle, and the whole stroke as a circle.</summary>
public static partial class ShapeCleanup
{
    private static (IReadOnlyList<GesturePoint> Points, PieceKind Kind) Fit(GesturePoint[] piece, double spacing)
    {
        if (IsLine(piece, 0, piece.Length - 1))
        {
            return ([piece[0], piece[^1]], PieceKind.Line);
        }

        if (FitCircle(piece) is { } circle && Sweep(piece, circle) is var sweep && Math.Abs(sweep) >= MinimumArcSweep && circle.MeanError <= ArcTolerance * circle.Radius)
        {
            return (ArcPoints(piece, circle, sweep, spacing), PieceKind.Arc);
        }

        return (Smooth(piece), PieceKind.Smooth);
    }

    private static bool IsLine(GesturePoint[] points, int from, int to)
    {
        var (start, end) = (points[from], points[to]);
        var chord = start.DistanceTo(end);
        if (chord <= 0)
        {
            return false;
        }

        var deviation = 0.0;
        for (var i = from + 1; i < to; i++)
        {
            // Distance from the chord: the cross product over the chord's length.
            deviation += Math.Abs(((end.X - start.X) * (start.Y - points[i].Y)) - ((start.X - points[i].X) * (end.Y - start.Y))) / chord;
        }

        return to - from < 2 || deviation / (to - from - 1) <= LineDeviation * chord;
    }

    private static bool IsArcLike(GesturePoint[] piece)
        => FitCircle(piece) is { } circle && circle.MeanError <= ArcTolerance * circle.Radius && Math.Abs(Sweep(piece, circle)) >= MinimumArcSweep;

    private static bool IsClosedCircle(GesturePoint[] points, Circle circle)
        => circle.MeanError <= ArcTolerance * circle.Radius
            && points[0].DistanceTo(points[^1]) <= ClosedGap * circle.Radius
            && Math.Abs(Sweep(points, circle)) >= 1.75 * Math.PI;

    /// <summary>The circle through the points by least squares (Kåsa's algebraic fit), with the mean distance of the points from it; null when they are collinear.</summary>
    private static Circle? FitCircle(GesturePoint[] points)
    {
        double meanX = points.Average(point => point.X), meanY = points.Average(point => point.Y);
        double suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
        foreach (var point in points)
        {
            var (u, v) = (point.X - meanX, point.Y - meanY);
            suu += u * u;
            svv += v * v;
            suv += u * v;
            suuu += u * u * u;
            svvv += v * v * v;
            suvv += u * v * v;
            svuu += v * u * u;
        }

        var determinant = (suu * svv) - (suv * suv);
        if (Math.Abs(determinant) < 1e-9)
        {
            return null;
        }

        var bu = 0.5 * (suuu + suvv);
        var bv = 0.5 * (svvv + svuu);
        var uc = ((bu * svv) - (bv * suv)) / determinant;
        var vc = ((bv * suu) - (bu * suv)) / determinant;
        var center = new GesturePoint(meanX + uc, meanY + vc);
        var radius = Math.Sqrt((uc * uc) + (vc * vc) + ((suu + svv) / points.Length));
        var error = points.Average(point => Math.Abs(point.DistanceTo(center) - radius));
        return radius > 0 && double.IsFinite(radius) ? new Circle(center, radius, error) : null;
    }

    /// <summary>The angle the points turn through around the circle's centre, signed (the drawn direction), unwrapped.</summary>
    private static double Sweep(GesturePoint[] points, Circle circle)
    {
        var total = 0.0;
        var previous = Angle(circle.Center, points[0]);
        for (var i = 1; i < points.Length; i++)
        {
            var angle = Angle(circle.Center, points[i]);
            var step = angle - previous;
            step -= 2 * Math.PI * Math.Round(step / (2 * Math.PI));
            total += step;
            previous = angle;
        }

        return total;
    }

    /// <summary>The arc from the piece's first point's angle through <paramref name="sweep"/>, its two ends put back on the drawn corners so pieces still join.</summary>
    private static List<GesturePoint> ArcPoints(GesturePoint[] piece, Circle circle, double sweep, double spacing)
    {
        var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) * circle.Radius / spacing));
        var start = Angle(circle.Center, piece[0]);
        var arc = new List<GesturePoint>(steps + 1) { piece[0] };
        for (var i = 1; i < steps; i++)
        {
            var angle = start + (sweep * i / steps);
            arc.Add(new GesturePoint(circle.Center.X + (circle.Radius * Math.Cos(angle)), circle.Center.Y + (circle.Radius * Math.Sin(angle))));
        }

        arc.Add(piece[^1]);
        return arc;
    }

    /// <summary>A full turn in the drawn direction, starting at the angle the stroke started at; it ends where it began.</summary>
    private static List<GesturePoint> CirclePoints(GesturePoint[] points, Circle circle, double spacing)
    {
        var direction = Math.Sign(Sweep(points, circle));
        var steps = Math.Max(8, (int)Math.Ceiling(2 * Math.PI * circle.Radius / spacing));
        var start = Angle(circle.Center, points[0]);
        var result = new List<GesturePoint>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var angle = start + (direction * 2 * Math.PI * i / steps);
            result.Add(new GesturePoint(circle.Center.X + (circle.Radius * Math.Cos(angle)), circle.Center.Y + (circle.Radius * Math.Sin(angle))));
        }

        return result;
    }

    private static double Angle(GesturePoint center, GesturePoint point) => Math.Atan2(point.Y - center.Y, point.X - center.X);

    private sealed record Circle(GesturePoint Center, double Radius, double MeanError);
}
