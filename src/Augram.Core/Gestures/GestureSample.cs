using System.Collections;

namespace Augram.Core.Gestures;

/// <summary>
/// One training sample of a gesture: the raw points exactly as drawn (CLAUDE.md
/// invariant 5). Immutable; the points are copied on construction. Resampling to the
/// active precision happens only at match time, in <c>Recognition</c>.
/// </summary>
public sealed class GestureSample : IReadOnlyList<GesturePoint>
{
    private readonly GesturePoint[] _points;

    public GestureSample(IEnumerable<GesturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToArray();
    }

    public int Count => _points.Length;

    public GesturePoint this[int index] => _points[index];

    public IEnumerator<GesturePoint> GetEnumerator() => ((IEnumerable<GesturePoint>)_points).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
