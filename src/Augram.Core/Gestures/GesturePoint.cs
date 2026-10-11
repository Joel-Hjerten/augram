namespace Augram.Core.Gestures;

/// <summary>
/// One raw cursor position of a stroke or a training sample, in whatever coordinate
/// space the capture produced (screen pixels today). Recognition is scale- and
/// position-invariant, so the space does not matter as long as X and Y share a unit.
/// </summary>
public readonly record struct GesturePoint(double X, double Y)
{
    /// <summary>
    /// The straight-line distance to <paramref name="other"/>: the one distance the recognizer's resampler
    /// (<c>GetPointArrayLength</c>'s per-segment term), shape cleanup and the starter set all use.
    /// </summary>
    public double DistanceTo(GesturePoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
