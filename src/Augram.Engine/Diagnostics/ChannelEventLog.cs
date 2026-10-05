using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Diagnostics;

/// <summary>
/// The Engine's <see cref="IEventLog"/>: <see cref="Log"/> does a level check and one non-blocking
/// <c>TryWrite</c> onto a bounded channel, then returns. A single drain task hands each event to every
/// <see cref="ILogSink"/> in order and flushes them whenever the queue runs empty. When the queue is
/// full the new event is dropped (the events already queued are the ones that explain the stall) and
/// counted; the drain task writes one Warning per <see cref="DropReportInterval"/> drops, and one more
/// when the burst ends. A sink that throws is counted in <see cref="SinkFailureCount"/> and skipped for
/// that event; nothing a sink does can reach the thread that logged. <see cref="Dispose"/> completes the
/// channel, waits for the drain to finish what is queued, then disposes the sinks it owns.
/// </summary>
public sealed class ChannelEventLog : IEventLog, IDisposable
{
    public const int DefaultCapacity = 4096;
    public const int DropReportInterval = 1000;
    public const string Source = "log";
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(10);

    private readonly Channel<LogEvent> _channel;
    private readonly ILogSink[] _sinks;
    private readonly Task _drain;
    private int _minimumLevel;
    private int _disposed;
    private long _dropped;
    private long _reportedDropped;
    private long _sinkFailures;

    public ChannelEventLog(IEnumerable<ILogSink> sinks, EventLevel minimumLevel = EventLevel.Info, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _sinks = [.. sinks];
        _minimumLevel = (int)minimumLevel;
        // TryWrite reports true even when DropWrite discards the item; only the callback sees a drop.
        _channel = Channel.CreateBounded<LogEvent>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            _ => Interlocked.Increment(ref _dropped));
        _drain = Task.Run(DrainAsync);
    }

    /// <summary>Changeable at runtime (Diagnostics tab); takes effect on the next call.</summary>
    public EventLevel MinimumLevel
    {
        get => (EventLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel, (int)value);
    }

    public long DroppedCount => Volatile.Read(ref _dropped);

    public long SinkFailureCount => Volatile.Read(ref _sinkFailures);

    public bool IsEnabled(EventLevel level) => level >= MinimumLevel && Volatile.Read(ref _disposed) == 0;

    public void Log(LogEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!IsEnabled(e.Level))
        {
            return;
        }

        _channel.Writer.TryWrite(e);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        try
        {
            _drain.Wait(DisposeTimeout);
        }
        catch (AggregateException)
        {
            // The drain loop guards every sink call; this only fires on a timeout. Nothing more to do.
        }

        foreach (var sink in _sinks)
        {
            Guarded(sink.Dispose);
        }
    }

    private async Task DrainAsync()
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var e))
            {
                Deliver(e);
                ReportDrops(force: false);
            }

            ReportDrops(force: true);
            foreach (var sink in _sinks)
            {
                Guarded(sink.Flush);
            }
        }
    }

    private void Deliver(LogEvent e)
    {
        foreach (var sink in _sinks)
        {
            Guarded(() => sink.Write(e));
        }
    }

    private void ReportDrops(bool force)
    {
        var dropped = Volatile.Read(ref _dropped);
        var unreported = dropped - _reportedDropped;
        if (unreported <= 0 || (!force && unreported < DropReportInterval))
        {
            return;
        }

        _reportedDropped = dropped;
        Deliver(new LogEvent(
            DateTimeOffset.Now,
            EventLevel.Warning,
            Source,
            "Events dropped: log queue full",
            [new("dropped", unreported), new("total", dropped)]));
    }

    private void Guarded(Action sinkCall)
    {
        try
        {
            sinkCall();
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _sinkFailures);
        }
    }
}
