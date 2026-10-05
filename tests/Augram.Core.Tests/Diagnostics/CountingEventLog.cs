using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Tests.Diagnostics;

/// <summary>Fake <see cref="IEventLog"/> that counts calls and keeps what it was given, with a switchable minimum level.</summary>
internal sealed class CountingEventLog : IEventLog
{
    private readonly List<LogEvent> _events = [];

    public CountingEventLog(EventLevel minimumLevel = EventLevel.Trace)
    {
        MinimumLevel = minimumLevel;
    }

    public EventLevel MinimumLevel { get; set; }

    public int IsEnabledCalls { get; private set; }

    public int LogCalls { get; private set; }

    public IReadOnlyList<LogEvent> Events => _events;

    public LogEvent Last => _events[^1];

    public bool IsEnabled(EventLevel level)
    {
        IsEnabledCalls++;
        return level >= MinimumLevel;
    }

    public void Log(LogEvent e)
    {
        LogCalls++;
        _events.Add(e);
    }
}
