namespace Augram.Core.Diagnostics;

/// <summary>
/// The store behind the recognition log panel (A15): the last <see cref="DefaultCapacity"/> strokes.
/// A plain <see cref="RingLog{T}"/>; the Engine worker adds one entry per completed stroke and also
/// emits the same facts to <see cref="Abstractions.IEventLog"/> at Info, so the file log and this panel agree.
/// </summary>
public sealed class RecognitionLog : RingLog<RecognitionLogEntry>
{
    public const int DefaultCapacity = 200;

    public RecognitionLog(int capacity = DefaultCapacity)
        : base(capacity)
    {
    }
}
