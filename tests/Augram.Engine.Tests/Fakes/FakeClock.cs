using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>A clock the test advances. <see cref="MonotonicMs"/> is read from the worker and the tick timer, so it is volatile.</summary>
internal sealed class FakeClock : IClock
{
    private long _monotonicMs;

    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    public long MonotonicMs
    {
        get => Volatile.Read(ref _monotonicMs);
        set => Volatile.Write(ref _monotonicMs, value);
    }

    public void Advance(long ms) => Interlocked.Add(ref _monotonicMs, ms);
}
