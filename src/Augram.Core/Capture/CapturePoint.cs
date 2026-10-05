namespace Augram.Core.Capture;

/// <summary>
/// A pointer position and the time it was observed. <paramref name="TimestampMs"/> is
/// milliseconds on whatever monotonic clock the Engine stamps events with; the state
/// machine only ever compares timestamps, never interprets them.
/// </summary>
public readonly record struct CapturePoint(int X, int Y, long TimestampMs)
{
    /// <summary>Squared distance, so thresholds can be compared without a square root on the hook thread.</summary>
    public long DistanceSquaredTo(CapturePoint other)
    {
        long dx = X - other.X;
        long dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    public bool IsSamePositionAs(CapturePoint other) => X == other.X && Y == other.Y;
}
