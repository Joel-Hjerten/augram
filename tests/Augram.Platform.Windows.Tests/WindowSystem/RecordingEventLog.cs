using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>Keeps every event so tests can assert on source, level and properties.</summary>
internal sealed class RecordingEventLog : IEventLog
{
    public List<LogEvent> Events { get; } = [];

    public bool IsEnabled(EventLevel level) => true;

    public void Log(LogEvent e) => Events.Add(e);

    public object? Property(LogEvent e, string key)
        => e.Properties?.FirstOrDefault(p => p.Key == key).Value;
}
