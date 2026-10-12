using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.TestSupport;

/// <summary>
/// A synchronous <see cref="IEventLog"/> for assertions: every enabled event lands in <see cref="Events"/> immediately.
/// Thread-safe (the worker, a start thread and the test thread may all use it). <see cref="MinimumLevel"/> (Trace by default)
/// is switchable; <see cref="IsEnabledCalls"/> and <see cref="LogCalls"/> count the calls, to prove a disabled level costs
/// nothing; <see cref="WaitForAny"/> waits for a write from another thread.
/// </summary>
public sealed class ListEventLog : IEventLog
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];
    private int _isEnabledCalls;
    private int _logCalls;

    public ListEventLog(EventLevel minimumLevel = EventLevel.Trace)
    {
        MinimumLevel = minimumLevel;
    }

    public EventLevel MinimumLevel { get; set; }

    public int IsEnabledCalls => Volatile.Read(ref _isEnabledCalls);

    public int LogCalls => Volatile.Read(ref _logCalls);

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

    public LogEvent Last => Events[^1];

    public bool IsEnabled(EventLevel level)
    {
        Interlocked.Increment(ref _isEnabledCalls);
        return level >= MinimumLevel;
    }

    public void Log(LogEvent e)
    {
        Interlocked.Increment(ref _logCalls);
        lock (_gate)
        {
            _events.Add(e);
        }
    }

    public bool Has(string source, string message) => Events.Any(e => e.Source == source && e.Message == message);

    public LogEvent Single(string source, string message) => Events.Single(e => e.Source == source && e.Message == message);

    public IReadOnlyList<LogEvent> From(string source) => Events.Where(e => e.Source == source).ToList();

    /// <summary>The value of property <paramref name="key"/> on <paramref name="e"/>, null when it has none.</summary>
    public static object? Property(LogEvent e, string key) => e.Properties?.FirstOrDefault(p => p.Key == key).Value;

    /// <summary>Polls every 10 ms until an event is in, or <paramref name="timeout"/> passes; the events either way.</summary>
    public IReadOnlyList<LogEvent> WaitForAny(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Events.Count == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        return Events;
    }
}
