using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>
/// A clock the test sets and advances. <see cref="MonotonicMs"/> is read from the worker and the tick timer, so it is volatile.
/// With <see cref="StepPerRead"/> set, every read of <see cref="UtcNow"/> first moves it on by that much (see
/// <see cref="Stepping"/>).
/// </summary>
public sealed class FakeClock : IClock
{
    private long _monotonicMs;
    private DateTimeOffset _utcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A clock one second further on at every read of <see cref="UtcNow"/>, from 2026-10-07 09:00 UTC: the machines of a sync
    /// test share one, so their files are ordered in time.
    /// </summary>
    public static FakeClock Stepping() => new() { UtcNow = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero), StepPerRead = TimeSpan.FromSeconds(1) };

    /// <summary>Added to <see cref="UtcNow"/> before each read; zero (a fixed time) by default.</summary>
    public TimeSpan StepPerRead { get; set; }

    public DateTimeOffset UtcNow
    {
        get
        {
            _utcNow += StepPerRead;
            return _utcNow;
        }

        set => _utcNow = value;
    }

    public long MonotonicMs
    {
        get => Volatile.Read(ref _monotonicMs);
        set => Volatile.Write(ref _monotonicMs, value);
    }

    public void Advance(long ms) => Interlocked.Add(ref _monotonicMs, ms);
}
