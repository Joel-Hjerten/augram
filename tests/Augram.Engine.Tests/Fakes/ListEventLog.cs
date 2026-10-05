using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Tests.Fakes;

/// <summary>A synchronous <see cref="IEventLog"/> for assertions: every enabled event lands in <see cref="Events"/> immediately.</summary>
internal sealed class ListEventLog : IEventLog
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];

    public ListEventLog(EventLevel minimumLevel = EventLevel.Trace)
    {
        MinimumLevel = minimumLevel;
    }

    public EventLevel MinimumLevel { get; }

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

    public bool IsEnabled(EventLevel level) => level >= MinimumLevel;

    public void Log(LogEvent e)
    {
        lock (_gate)
        {
            _events.Add(e);
        }
    }

    public bool Has(string source, string message) => Events.Any(e => e.Source == source && e.Message == message);

    public LogEvent Single(string source, string message) => Events.Single(e => e.Source == source && e.Message == message);

    public IReadOnlyList<LogEvent> From(string source) => Events.Where(e => e.Source == source).ToList();
}
