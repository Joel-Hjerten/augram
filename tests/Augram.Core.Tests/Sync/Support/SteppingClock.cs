using Augram.Core.Abstractions;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>A clock one second further on at every read, shared by the machines of a test so their files are ordered in time.</summary>
internal sealed class SteppingClock : IClock
{
    private DateTimeOffset _now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow
    {
        get
        {
            _now = _now.AddSeconds(1);
            return _now;
        }
    }

    public long MonotonicMs => 0;
}
