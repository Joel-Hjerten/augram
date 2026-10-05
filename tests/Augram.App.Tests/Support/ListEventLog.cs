using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.App.Tests.Support;

/// <summary>A synchronous <see cref="IEventLog"/> for assertions: every event lands in <see cref="Events"/> at once.</summary>
internal sealed class ListEventLog : IEventLog
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];

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

    public bool IsEnabled(EventLevel level) => true;

    public void Log(LogEvent e)
    {
        lock (_gate)
        {
            _events.Add(e);
        }
    }

    public bool Has(string source, string message) => Events.Any(e => e.Source == source && e.Message == message);
}
