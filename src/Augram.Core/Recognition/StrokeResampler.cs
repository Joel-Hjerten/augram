using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>
/// Resamples a raw point list to at most <c>segments</c> points spaced evenly along the
/// polyline's arc length. Port of StrokesPlus classic <c>GetInterpolatedPointArray</c>
/// (StrokesPlusHook.cpp 5982–6059). The loop is kept structurally identical on purpose,
/// including the <c>currentIndex--</c> revisit of a raw segment that is longer than one
/// resampled step and the early break once <c>segments</c> points exist: the original
/// comment notes that rounding can otherwise try to emit one point too many. In double
/// arithmetic every stroke with two distinct points resamples to exactly <c>segments</c>
/// points (tested); the original used float coordinates, where one fewer is possible.
/// </summary>
public static class StrokeResampler
{
    public static GesturePoint[] Resample(IReadOnlyList<GesturePoint> points, int segments)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 1);

        if (points.Count == 0)
        {
            return [];
        }

        double totalLength = PathLength(points);
        if (totalLength <= 0)
        {
            // Every point coincides: there is no direction to sample. The original would
            // divide by a zero segment length here; we return the lone point instead, which
            // yields an empty angle sequence and therefore a score of 0.
            return [points[0]];
        }

        var interpolatedPoints = new List<GesturePoint>(segments);
        double desiredSegmentLength = totalLength / segments;
        double currentSegmentLength = 0;

        var lastTestPoint = points[0];
        interpolatedPoints.Add(lastTestPoint);

        for (int currentIndex = 1; currentIndex < points.Count; currentIndex++)
        {
            var currentPoint = points[currentIndex];

            double incToCurrentLength = Distance(lastTestPoint, currentPoint);
            double testSegmentLength = currentSegmentLength + incToCurrentLength;

            if (testSegmentLength < desiredSegmentLength)
            {
                // Desired length not reached yet: absorb this raw segment and move on.
                currentSegmentLength = testSegmentLength;
                lastTestPoint = currentPoint;
                continue;
            }

            // Reached or overshot: interpolate the point that lands exactly on the step.
            double interpolationPosition = (desiredSegmentLength - currentSegmentLength) * (1 / incToCurrentLength);
            var interpolatedPoint = Interpolate(lastTestPoint, currentPoint, interpolationPosition);
            interpolatedPoints.Add(interpolatedPoint);

            if (interpolatedPoints.Count == segments)
            {
                break;
            }

            // Continue from the new point along the same raw segment.
            lastTestPoint = interpolatedPoint;
            currentSegmentLength = 0;
            currentIndex--;
        }

        return interpolatedPoints.ToArray();
    }

    /// <summary>Sum of the segment lengths (<c>GetPointArrayLength</c>).</summary>
    public static double PathLength(IReadOnlyList<GesturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double length = 0;
        for (int i = 1; i < points.Count; i++)
        {
            length += Distance(points[i - 1], points[i]);
        }

        return length;
    }

    private static double Distance(GesturePoint a, GesturePoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static GesturePoint Interpolate(GesturePoint start, GesturePoint end, double position)
    {
        return new GesturePoint(
            ((1 - position) * start.X) + (position * end.X),
            ((1 - position) * start.Y) + (position * end.Y));
    }
}
