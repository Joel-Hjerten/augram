using Augram.Core.Diagnostics;

namespace Augram.Engine.Diagnostics;

/// <summary>
/// Where <see cref="ChannelEventLog"/> delivers drained events. <see cref="Write"/> and <see cref="Flush"/>
/// are called from the drain task only, one event at a time, so a sink needs no lock of its own unless it
/// has another thread (a flush timer). A sink may be slow or throw; the channel absorbs both and never
/// lets either reach the thread that logged.
/// </summary>
public interface ILogSink : IDisposable
{
    void Write(LogEvent e);

    /// <summary>Called when the queue has been drained to empty; a buffered sink pushes bytes to disk here.</summary>
    void Flush();
}
