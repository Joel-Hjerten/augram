using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.Core.Tests.Diagnostics;

/// <summary>The call-site sugar builds a <see cref="LogEvent"/> only when the level is enabled (N4: allocation-free on the hot path).</summary>
public sealed class EventLogExtensionsTests
{
    [Fact]
    public void DisabledLevel_ChecksIsEnabledAndNeverCallsLog()
    {
        var log = new CountingEventLog(EventLevel.Info);

        log.Trace("hook", "move", ("x", "1"));
        log.Debug("hook", "move");

        Assert.Equal(2, log.IsEnabledCalls);
        Assert.Equal(0, log.LogCalls);
    }

    [Fact]
    public void DisabledLevel_AllocatesNothing()
    {
        var log = new CountingEventLog(EventLevel.Info);
        log.Trace("hook", "warm-up", ("k", "v"));

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            log.Trace("hook", "move", ("k", "v"));
            log.Debug("hook", "move");
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(0, log.LogCalls);
    }

    [Fact]
    public void EnabledLevel_BuildsTheEventWithLevelSourceMessageAndProperties()
    {
        var log = new CountingEventLog();
        var start = DateTimeOffset.Now;

        log.Info("hook", "Hook installed", ("generation", 2), ("thread", "hook-gen2"));

        var e = log.Last;
        Assert.Equal(EventLevel.Info, e.Level);
        Assert.Equal("hook", e.Source);
        Assert.Equal("Hook installed", e.Message);
        Assert.NotNull(e.Properties);
        Assert.Equal(["generation", "thread"], e.Properties.Select(p => p.Key));
        Assert.Equal([2, "hook-gen2"], e.Properties.Select(p => p.Value));
        Assert.Null(e.Exception);
        Assert.InRange(e.Timestamp, start, DateTimeOffset.Now);
    }

    [Fact]
    public void NoProperties_LeavesPropertiesNull()
    {
        var log = new CountingEventLog();

        log.Warning("config", "Migrated");

        Assert.Null(log.Last.Properties);
        Assert.Equal(EventLevel.Warning, log.Last.Level);
    }

    [Fact]
    public void Error_CarriesTheException()
    {
        var log = new CountingEventLog();
        var boom = new InvalidOperationException("boom");

        log.Error("steps", "Step failed", boom, ("step", "Hotkey"));
        log.Error("steps", "Step failed without exception");

        Assert.Same(boom, log.Events[0].Exception);
        Assert.Equal(EventLevel.Error, log.Events[0].Level);
        Assert.Null(log.Events[1].Exception);
    }

    [Theory]
    [InlineData(EventLevel.Trace)]
    [InlineData(EventLevel.Debug)]
    [InlineData(EventLevel.Info)]
    [InlineData(EventLevel.Warning)]
    [InlineData(EventLevel.Error)]
    public void EachMethod_UsesItsLevel(EventLevel level)
    {
        var log = new CountingEventLog();

        switch (level)
        {
            case EventLevel.Trace:
                log.Trace("s", "m");
                break;
            case EventLevel.Debug:
                log.Debug("s", "m");
                break;
            case EventLevel.Info:
                log.Info("s", "m");
                break;
            case EventLevel.Warning:
                log.Warning("s", "m");
                break;
            default:
                log.Error("s", "m");
                break;
        }

        Assert.Equal(level, log.Last.Level);
    }

    [Fact]
    public void NullEventLog_IsNeverEnabledAndAcceptsEverything()
    {
        var log = NullEventLog.Instance;

        Assert.False(log.IsEnabled(EventLevel.Error));
        log.Log(new LogEvent(DateTimeOffset.Now, EventLevel.Error, "s", "m"));
        log.Error("s", "m", new InvalidOperationException());
    }
}
