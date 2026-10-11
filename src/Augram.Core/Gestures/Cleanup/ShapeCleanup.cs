using Augram.Core.Recognition;

namespace Augram.Core.Gestures.Cleanup;

/// <summary>
/// Gesture shape cleanup (requirements F3 "Shape cleanup", plan 0001 M2 step 10; Joel 2026-10-08): wobbly near-straight
/// runs become straight lines, uneven curves smooth arcs (a closed stroke a circle), anything else is gently smoothed. The
/// stroke is resampled evenly, corners are found with ShortStraw (Wolin et al. 2008: a corner is where the "straw", the
/// distance between the points a few steps before and after, is shortest), each piece between two corners is fitted, and
/// the result is again a raw point list (invariant 5) in the drawn direction and angle: a tilted line stays tilted, since
/// recognition is rotation-sensitive (invariant 3). Pure and deterministic; the original stroke is the caller's to keep.
/// </summary>
public static partial class ShapeCleanup
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
        var length = StrokeResampler.PathLength(stroke);
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
            var segment = from.DistanceTo(to);
            while (segment > 0 && carried + segment >= spacing)
            {
                var t = (spacing - carried) / segment;
                from = new GesturePoint(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
                result.Add(from);
                segment = from.DistanceTo(to);
                carried = 0;
            }

            carried += segment;
        }

        if (result[^1].DistanceTo(stroke[^1]) > spacing / 4)
        {
            result.Add(stroke[^1]);
        }
        else
        {
            result[^1] = stroke[^1];
        }

        return [.. result];
    }

    private static double Diagonal(IReadOnlyList<GesturePoint> stroke)
    {
        double minX = stroke.Min(point => point.X), maxX = stroke.Max(point => point.X);
        double minY = stroke.Min(point => point.Y), maxY = stroke.Max(point => point.Y);
        return Math.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)));
    }
}
