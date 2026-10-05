using Augram.Core.Abstractions;

namespace Augram.Core.Diagnostics;

/// <summary>Discards everything and reports every level disabled. The default for tests and for components built before the composition root wires a real log.</summary>
public sealed class NullEventLog : IEventLog
{
    private NullEventLog()
    {
    }

    public static NullEventLog Instance { get; } = new();

    public bool IsEnabled(EventLevel level) => false;

    public void Log(LogEvent e)
    {
    }
}
