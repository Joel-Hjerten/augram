using System.Diagnostics;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Xunit;

namespace Augram.Engine.Tests.Diagnostics;

/// <summary>N4: the caller never blocks, order is kept, drops are counted and reported, dispose drains.</summary>
public sealed class ChannelEventLogTests
{
    private const int MaxCallMs = 100;

    [Fact]
    public void Events_ReachEverySinkInOrder()
    {
        var first = new CollectingSink();
        var second = new CollectingSink();
        using (var log = new ChannelEventLog([first, second]))
        {
            for (var i = 0; i < 5000; i++)
            {
                log.Info("test", "event", ("n", i));
            }
        }

        foreach (var sink in new[] { first, second })
        {
            var logged = sink.Logged;
            Assert.Equal(5000, logged.Count);
            Assert.Equal(Enumerable.Range(0, 5000), logged.Select(e => (int)e.Properties![0].Value!));
            Assert.True(sink.Disposed);
            Assert.True(sink.FlushCount >= 1);
        }
    }

    [Fact]
    public void SlowSink_NeverBlocksTheCallerAndDropsAreCounted()
    {
        using var gate = new ManualResetEventSlim(false);
        var sink = new CollectingSink { Gate = gate };
        var log = new ChannelEventLog([sink], capacity: 4096);
        var maxCallMs = 0.0;
        var producer = new Thread(() =>
        {
            var watch = new Stopwatch();
            for (var i = 0; i < 10_000; i++)
            {
                watch.Restart();
                log.Info("test", "burst", ("n", i));
                maxCallMs = Math.Max(maxCallMs, watch.Elapsed.TotalMilliseconds);
            }
        });

        var total = Stopwatch.StartNew();
        producer.Start();
        Assert.True(producer.Join(TimeSpan.FromSeconds(5)), "producer thread was blocked by the log");
        total.Stop();

        Assert.True(maxCallMs < MaxCallMs, $"slowest Log call took {maxCallMs:F1} ms");
        Assert.True(total.ElapsedMilliseconds < 2000, $"10,000 calls took {total.ElapsedMilliseconds} ms");
        Assert.True(log.DroppedCount > 0, "expected drops with the sink blocked");

        gate.Set();
        log.Dispose();

        var delivered = sink.Logged;
        Assert.True(delivered.Count >= 4096, $"only {delivered.Count} delivered");
        Assert.Equal(10_000, delivered.Count + log.DroppedCount);
        Assert.Equal(Enumerable.Range(0, delivered.Count), delivered.Select(e => (int)e.Properties![0].Value!));
    }

    [Fact]
    public void Drops_AreReportedAsAWarningFromTheLogSource()
    {
        using var gate = new ManualResetEventSlim(false);
        var sink = new CollectingSink { Gate = gate };
        var log = new ChannelEventLog([sink], capacity: 8);
        for (var i = 0; i < 100; i++)
        {
            log.Info("test", "burst");
        }

        gate.Set();
        log.Dispose();

        var report = Assert.Single(sink.DropReports);
        Assert.Equal(EventLevel.Warning, report.Level);
        Assert.Equal(ChannelEventLog.Source, report.Source);
        Assert.Equal(log.DroppedCount, (long)report.Properties![0].Value!);
        Assert.Equal(log.DroppedCount, (long)report.Properties![1].Value!);
        Assert.Same(sink.Events[^1], report);
    }

    [Fact]
    public void MinimumLevel_FiltersWithoutCountingAsDroppedAndChangesAtRuntime()
    {
        var sink = new CollectingSink();
        using (var log = new ChannelEventLog([sink], EventLevel.Info))
        {
            Assert.False(log.IsEnabled(EventLevel.Debug));
            Assert.True(log.IsEnabled(EventLevel.Info));
            log.Debug("test", "hidden");
            log.Info("test", "shown");

            log.MinimumLevel = EventLevel.Trace;
            Assert.True(log.IsEnabled(EventLevel.Trace));
            log.Debug("test", "now shown");

            Assert.Equal(0, log.DroppedCount);
        }

        Assert.Equal(["shown", "now shown"], sink.Logged.Select(e => e.Message));
    }

    [Fact]
    public void ThrowingSink_IsCountedAndTheOthersStillGetEverything()
    {
        var broken = new CollectingSink { ThrowOnWrite = true };
        var good = new CollectingSink();
        var log = new ChannelEventLog([broken, good]);
        for (var i = 0; i < 50; i++)
        {
            log.Error("test", "boom", new InvalidOperationException("x"));
        }

        log.Dispose();

        Assert.Equal(50, good.Logged.Count);
        Assert.Empty(broken.Events);
        Assert.Equal(50, log.SinkFailureCount);
        Assert.True(broken.Disposed);
    }

    [Fact]
    public void AfterDispose_LoggingIsDisabledAndHarmless()
    {
        var sink = new CollectingSink();
        var log = new ChannelEventLog([sink]);
        log.Info("test", "before");
        log.Dispose();

        Assert.False(log.IsEnabled(EventLevel.Error));
        log.Error("test", "after");
        log.Log(new LogEvent(DateTimeOffset.Now, EventLevel.Error, "test", "direct"));
        log.Dispose();

        Assert.Equal(["before"], sink.Logged.Select(e => e.Message));
        Assert.Equal(0, log.DroppedCount);
    }

    [Fact]
    public void BadArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ChannelEventLog(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChannelEventLog([], capacity: 0));
    }
}
