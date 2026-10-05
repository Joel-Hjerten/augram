using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;

namespace Augram.Engine.Tests.Diagnostics;

/// <summary>
/// Test sink that records everything it is given. Can be made slow (<see cref="Gate"/>: every write
/// blocks until the gate is set) or broken (<see cref="ThrowOnWrite"/>) to exercise the channel.
/// </summary>
internal sealed class CollectingSink : ILogSink
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];

    public ManualResetEventSlim? Gate { get; init; }

    public bool ThrowOnWrite { get; init; }

    public int FlushCount { get; private set; }

    public bool Disposed { get; private set; }

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>Events the test itself logged, excluding the channel's own drop reports.</summary>
    public IReadOnlyList<LogEvent> Logged => Events.Where(e => e.Source != ChannelEventLog.Source).ToList();

    public IReadOnlyList<LogEvent> DropReports => Events.Where(e => e.Source == ChannelEventLog.Source).ToList();

    public void Write(LogEvent e)
    {
        Gate?.Wait();
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("broken sink");
        }

        lock (_gate)
        {
            _events.Add(e);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            FlushCount++;
        }
    }

    public void Dispose() => Disposed = true;
}
