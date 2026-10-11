namespace Augram.Core.Gestures.Cleanup;

/// <summary>Shape cleanup, the corners: ShortStraw's straws, the two-scale turn test, and the post-processing that adds missed corners and drops straight ones.</summary>
public static partial class ShapeCleanup
{
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
            straws[i] = points[i - StrawWindow].DistanceTo(points[i + StrawWindow]);
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

    private static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
    }
}
