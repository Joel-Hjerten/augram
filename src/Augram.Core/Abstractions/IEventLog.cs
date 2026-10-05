using Augram.Core.Diagnostics;

namespace Augram.Core.Abstractions;

/// <summary>
/// The structured event log port (N4). <see cref="Log"/> must never block the caller: the Engine's
/// implementation enqueues and returns, a worker writes. Call <see cref="IsEnabled"/> before building
/// anything expensive; the extension methods in <see cref="EventLogExtensions"/> already do.
/// </summary>
public interface IEventLog
{
    bool IsEnabled(EventLevel level);

    void Log(LogEvent e);
}
