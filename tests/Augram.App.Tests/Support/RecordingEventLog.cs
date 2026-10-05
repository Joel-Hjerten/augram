using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.App.Tests.Support;

public sealed class RecordingEventLog : IEventLog
{
    public List<LogEvent> Events { get; } = [];

    public bool IsEnabled(EventLevel level) => true;

    public void Log(LogEvent e) => Events.Add(e);
}
