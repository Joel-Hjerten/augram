using Augram.Core.Diagnostics;

namespace Augram.Engine.Diagnostics;

/// <summary>
/// The last <see cref="DefaultCapacity"/> events for the Diagnostics tab's live tail (N4). A
/// <see cref="RingLog{T}"/>, so the view polls <see cref="RingLog{T}.Version"/> or subscribes to
/// <see cref="RingLog{T}.Changed"/> (raised on the drain thread) and takes a <see cref="RingLog{T}.Snapshot"/>.
/// Filtering by level or source is the view's job over the snapshot.
/// </summary>
public sealed class InMemorySink : RingLog<LogEvent>, ILogSink
{
    public const int DefaultCapacity = 2000;

    public InMemorySink(int capacity = DefaultCapacity)
        : base(capacity)
    {
    }

    public void Write(LogEvent e) => Add(e);

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
