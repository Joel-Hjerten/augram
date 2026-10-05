using System.Diagnostics;

namespace Augram.Core.Abstractions;

/// <summary>The real clock: <see cref="Stopwatch"/> ticks since process start for the monotonic part.</summary>
public sealed class SystemClock : IClock
{
    private static readonly long Start = Stopwatch.GetTimestamp();

    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long MonotonicMs => (Stopwatch.GetTimestamp() - Start) * 1000 / Stopwatch.Frequency;
}
