using Augram.Core.Gestures;

namespace Augram.Core.Capture;

/// <summary>The one hand-over from a captured stroke to the gesture model: what recognition ranks and training stores.</summary>
public static class CapturePointExtensions
{
    /// <summary>The stroke's positions as <see cref="GesturePoint"/>s, in order; timestamps are dropped. A fresh array each call.</summary>
    public static GesturePoint[] ToGesturePoints(this IReadOnlyList<CapturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var result = new GesturePoint[points.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new GesturePoint(points[i].X, points[i].Y);
        }

        return result;
    }
}
