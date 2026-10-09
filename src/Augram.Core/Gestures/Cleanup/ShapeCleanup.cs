namespace Augram.Core.Gestures.Cleanup;

/// <summary>How one piece of a cleaned stroke was fitted.</summary>
public enum PieceKind
{
    /// <summary>A near-straight run, replaced by the straight line between its ends (its angle kept, never snapped).</summary>
    Line,

    /// <summary>A curve close to a circular arc, replaced by that arc, swept in the drawn direction.</summary>
    Arc,

    /// <summary>The whole stroke closes on itself like a circle: a full circle from where it started, in the drawn direction.</summary>
    Circle,

    /// <summary>Neither: the drawn points, gently smoothed, ends kept.</summary>
    Smooth,
}

/// <summary>A cleaned stroke: the new raw points, the corners found (where the pieces meet) and each piece's kind, in order.</summary>
public sealed record CleanedShape(IReadOnlyList<GesturePoint> Points, IReadOnlyList<GesturePoint> Corners, IReadOnlyList<PieceKind> Pieces);

/// <summary>
/// Gesture shape cleanup (requirements F3 "Shape cleanup", plan 0001 M2 step 10; Joel 2026-10-08): wobbly near-straight
/// runs become straight lines, uneven curves smooth arcs (a closed stroke a circle), anything else is gently smoothed. The
/// stroke is resampled evenly, corners are found with ShortStraw (Wolin et al. 2008: a corner is where the "straw", the
/// distance between the points a few steps before and after, is shortest), each piece between two corners is fitted, and
/// the result is again a raw point list (invariant 5) in the drawn direction and angle: a tilted line stays tilted, since
/// recognition is rotation-sensitive (invariant 3). Pure and deterministic; the original stroke is the caller's to keep.
/// </summary>
public static class ShapeCleanup
{
    /// <summary>The analysis spacing is the stroke's bounding-box diagonal over this (ShortStraw's 40).</summary>
    public const int AnalysisPoints = 40;

    /// <summary>A corner must turn the stroke at least this much (radians) over the wide window, so a wiggle that turns back is none.</summary>
    public const double MinimumCornerTurn = 35 * Math.PI / 180;

    /// <summary>
    /// A corner turns sharply: at least this share of the wide window's turn happens within the narrow one. A round curve
    /// spreads its turn evenly (about half), a corner concentrates it (close to all) (Joel, 2026-10-09: corners showed on
    /// small wiggles and on large rounded shapes).
    /// </summary>
    public const double CornerSharpness = 0.7;

    /// <summary>ShortStraw's window: the straw at a point spans this many resampled points either side.</summary>
    public const int StrawWindow = 3;

    /// <summary>A straw this much shorter than the median marks a corner candidate (ShortStraw's 0.95).</summary>
    public const double CornerThreshold = 0.95;

    /// <summary>A piece is straight when its points stray from the line between its ends by at most this share of that line's length, on average (a path-length ratio is thrown off by the wobble itself).</summary>
    public const double LineDeviation = 0.03;

    /// <summary>A piece is an arc when its points stray from the fitted circle by at most this share of the radius, on average.</summary>
    public const double ArcTolerance = 0.08;

    /// <summary>A piece must sweep at least this much (radians) to count as an arc; flatter ones are lines or smoothed.</summary>
    public const double MinimumArcSweep = Math.PI / 6;

    /// <summary>A whole stroke closes into a circle when its ends meet within this share of the radius and it sweeps nearly a full turn.</summary>
    public const double ClosedGap = 0.35;

    public static CleanedShape Clean(IReadOnlyList<GesturePoint> stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        var length = PathLength(stroke);
        if (stroke.Count < 3 || length < 1)
        {
            return new CleanedShape([.. stroke], [], []);
        }

        var spacing = Math.Max(Diagonal(stroke) / AnalysisPoints, length / (AnalysisPoints * 4));
        var points = ResampleEvenly(stroke, spacing);

        // Corners are looked for on a lightly smoothed copy, so a hand's wobble does not make straws short; the pieces are
        // fitted on the points as drawn.
        var corners = Corners(Smooth(Smooth(points)));

        // One piece that closes on itself is a circle, drawn from where it started and in its direction.
        if (corners.Count == 2 && FitCircle(points) is { } whole && IsClosedCircle(points, whole))
        {
            return new CleanedShape(CirclePoints(points, whole, spacing), [], [PieceKind.Circle]);
        }

        var result = new List<GesturePoint>();
        var pieces = new List<PieceKind>();
        for (var i = 0; i + 1 < corners.Count; i++)
        {
            var piece = points[corners[i]..(corners[i + 1] + 1)];
            var (fitted, kind) = Fit(piece, spacing);
            result.AddRange(result.Count == 0 ? fitted : fitted.Skip(1));
            pieces.Add(kind);
        }

        return new CleanedShape(result, [.. corners.Skip(1).SkipLast(1).Select(index => points[index])], pieces);
    }

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

    /// <summary>ShortStraw's corners (indices, the two ends included), with its two refinements: a corner is added where a piece is not straight, and a corner between two pieces that make one straight line is dropped.</summary>
    private static List<int> Corners(GesturePoint[] points)
    {
        var corners = new List<int> { 0 };
        var count = points.Length;
        if (count <= (2 * StrawWindow) + 1)
        {
            corners.Add(count - 1);
            return corners;
        }

        var straws = new double[count];
        for (var i = StrawWindow; i < count - StrawWindow; i++)
        {
            straws[i] = Distance(points[i - StrawWindow], points[i + StrawWindow]);
        }

        var inner = straws[StrawWindow..(count - StrawWindow)];
        var threshold = Median(inner) * CornerThreshold;
        for (var i = StrawWindow; i < count - StrawWindow; i++)
        {
            if (straws[i] >= threshold || !IsSharpTurn(points, i))
            {
                continue;
            }

            // The shortest straw of this run below the threshold is the corner.
            var best = i;
            while (i + 1 < count - StrawWindow && straws[i + 1] < threshold)
            {
                i++;
                if (straws[i] < straws[best])
                {
                    best = i;
                }
            }

            corners.Add(best);
        }

        corners.Add(count - 1);
        AddMissedCorners(points, corners, straws);
        DropStraightCorners(points, corners);
        return corners;
    }

    /// <summary>Between two corners that do not make a line, the shortest straw in the middle part is one more corner (ShortStraw's post-processing).</summary>
    private static void AddMissedCorners(GesturePoint[] points, List<int> corners, double[] straws)
    {
        for (var pass = 0; pass < AnalysisPoints; pass++)
        {
            var added = false;
            for (var i = 1; i < corners.Count; i++)
            {
                var (from, to) = (corners[i - 1], corners[i]);
                if (to - from <= 2 * StrawWindow || IsLine(points, from, to) || IsArcLike(points[from..(to + 1)]))
                {
                    continue;
                }

                var low = from + ((to - from) / 4);
                var high = to - ((to - from) / 4);
                var best = -1;
                for (var j = Math.Max(low, StrawWindow); j <= Math.Min(high, points.Length - StrawWindow - 1); j++)
                {
                    if ((best < 0 || straws[j] < straws[best]) && IsSharpTurn(points, j))
                    {
                        best = j;
                    }
                }

                if (best > from && best < to)
                {
                    corners.Insert(i, best);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                return;
            }
        }
    }

    /// <summary>A corner whose two neighbours make one straight line with it is not a corner.</summary>
    private static void DropStraightCorners(GesturePoint[] points, List<int> corners)
    {
        for (var i = 1; i + 1 < corners.Count;)
        {
            if (IsLine(points, corners[i - 1], corners[i + 1]))
            {
                corners.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }

    private static bool IsLine(GesturePoint[] points, int from, int to)
    {
        var (start, end) = (points[from], points[to]);
        var chord = Distance(start, end);
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
            && Distance(points[0], points[^1]) <= ClosedGap * circle.Radius
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
        var error = points.Average(point => Math.Abs(Distance(point, center) - radius));
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

    /// <summary>Two passes of a three-point moving average, the ends kept where they were drawn.</summary>
    private static GesturePoint[] Smooth(GesturePoint[] piece)
    {
        var current = piece;
        for (var pass = 0; pass < 2; pass++)
        {
            var next = new GesturePoint[current.Length];
            next[0] = current[0];
            next[^1] = current[^1];
            for (var i = 1; i < current.Length - 1; i++)
            {
                next[i] = new GesturePoint(
                    (current[i - 1].X + current[i].X + current[i + 1].X) / 3,
                    (current[i - 1].Y + current[i].Y + current[i + 1].Y) / 3);
            }

            current = next;
        }

        return current;
    }

    /// <summary>Points every <paramref name="spacing"/> along the stroke, the first and last kept.</summary>
    internal static GesturePoint[] ResampleEvenly(IReadOnlyList<GesturePoint> stroke, double spacing)
    {
        var result = new List<GesturePoint> { stroke[0] };
        var carried = 0.0;
        for (var i = 1; i < stroke.Count; i++)
        {
            var from = stroke[i - 1];
            var to = stroke[i];
            var segment = Distance(from, to);
            while (segment > 0 && carried + segment >= spacing)
            {
                var t = (spacing - carried) / segment;
                from = new GesturePoint(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
                result.Add(from);
                segment = Distance(from, to);
                carried = 0;
            }

            carried += segment;
        }

        if (Distance(result[^1], stroke[^1]) > spacing / 4)
        {
            result.Add(stroke[^1]);
        }
        else
        {
            result[^1] = stroke[^1];
        }

        return [.. result];
    }

    private static double PathLength(IReadOnlyList<GesturePoint> stroke)
    {
        var length = 0.0;
        for (var i = 1; i < stroke.Count; i++)
        {
            length += Distance(stroke[i - 1], stroke[i]);
        }

        return length;
    }

    /// <summary>
    /// A corner at <paramref name="i"/> judged at two scales: the stroke turns at least <see cref="MinimumCornerTurn"/> over
    /// twice ShortStraw's window (a wiggle turns back, so not there), and most of that turn happens within the window itself
    /// (<see cref="CornerSharpness"/>; a round curve spreads it). Near the ends, where the wide window does not fit, the
    /// narrow turn alone decides.
    /// </summary>
    private static bool IsSharpTurn(GesturePoint[] points, int i)
    {
        var narrow = Turn(points, i, StrawWindow);
        var wideWindow = Math.Min(2 * StrawWindow, Math.Min(i, points.Length - 1 - i));
        if (wideWindow <= StrawWindow)
        {
            return narrow >= MinimumCornerTurn;
        }

        var wide = Turn(points, i, wideWindow);
        return wide >= MinimumCornerTurn && narrow >= CornerSharpness * wide;
    }

    /// <summary>How much the stroke turns at point <paramref name="i"/>: the angle between the run into it and the run out of it, <paramref name="window"/> points either side.</summary>
    private static double Turn(GesturePoint[] points, int i, int window)
    {
        var (before, at, after) = (points[i - window], points[i], points[i + window]);
        var turn = Math.Atan2(after.Y - at.Y, after.X - at.X) - Math.Atan2(at.Y - before.Y, at.X - before.X);
        return Math.Abs(turn - (2 * Math.PI * Math.Round(turn / (2 * Math.PI))));
    }

    private static double Diagonal(IReadOnlyList<GesturePoint> stroke)
    {
        double minX = stroke.Min(point => point.X), maxX = stroke.Max(point => point.X);
        double minY = stroke.Min(point => point.Y), maxY = stroke.Max(point => point.Y);
        return Math.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)));
    }

    private static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static double Distance(GesturePoint a, GesturePoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static double Angle(GesturePoint center, GesturePoint point) => Math.Atan2(point.Y - center.Y, point.X - center.X);

    private sealed record Circle(GesturePoint Center, double Radius, double MeanError);
}
