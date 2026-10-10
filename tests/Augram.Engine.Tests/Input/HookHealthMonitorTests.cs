using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using Augram.Engine.Input;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>Reinstall on the source's loss signal and on the cursor watchdog; logs every transition; system events restart the silence clock.</summary>
public sealed class HookHealthMonitorTests
{
    private static readonly TimeSpan LongPoll = TimeSpan.FromHours(1);

    [Fact]
    public void Start_InstallsOnce_AndLogsInstalled()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        using var monitor = new HookHealthMonitor(source, new FakeClock(), log, pollInterval: LongPoll);

        monitor.Start((in RawInput _) => false);

        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, monitor.Generation);
        Assert.Equal(0, monitor.ReinstallCount);
        Assert.True(log.Has(LogSources.Hook, "Hook installing"));
        Assert.True(log.Has(LogSources.Hook, "Hook installed"));
        Assert.Throws<InvalidOperationException>(() => monitor.Start((in RawInput _) => false));
    }

    [Fact]
    public void LostSignal_ReinstallsOnNextPoll_AndRequestsAReset()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var clock = new FakeClock();
        using var monitor = new HookHealthMonitor(source, clock, log, pollInterval: LongPoll);
        var resets = new List<string>();
        monitor.ResetRequested += (_, reason) => resets.Add(reason);
        monitor.Start((in RawInput _) => false);

        source.ReportLost("HookDisabled");
        Assert.Equal(1, source.StartCount);
        Assert.True(log.Has(LogSources.Hook, "Hook lost"));

        monitor.Poll();

        Assert.Equal(2, source.StartCount);
        Assert.Equal(2, monitor.Generation);
        Assert.Equal(1, monitor.ReinstallCount);
        Assert.True(source.IsRunning);
        Assert.Single(resets);
        Assert.True(log.Has(LogSources.Hook, "Hook lost, reinstalling"));
        var reinstalled = log.Single(LogSources.Hook, "Hook reinstalled");
        Assert.Equal(EventLevel.Info, reinstalled.Level);
        Assert.Contains(reinstalled.Properties!, p => p.Key == "generation" && (int)p.Value! == 2);

        monitor.Poll();
        Assert.Equal(2, source.StartCount);
    }

    [Fact]
    public void Watchdog_ReinstallsWhenTheCursorMovesButNoEventsArrive()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var clock = new FakeClock();
        var cursor = new FakeCursorProbe();
        using var monitor = new HookHealthMonitor(source, clock, log, cursor, pollInterval: LongPoll);
        monitor.Start((in RawInput _) => false);

        for (var i = 0; i < HookHealthMonitor.MinCursorMoves + 2; i++)
        {
            clock.Advance(HookHealthMonitor.DeadAfterMs);
            cursor.X += 10;
            monitor.Poll();
        }

        Assert.Equal(1, monitor.ReinstallCount);
        Assert.Equal(2, source.StartCount);
        Assert.Contains("cursor moved", (string)log.Single(LogSources.Hook, "Hook lost, reinstalling").Properties!.Single(p => p.Key == "reason").Value!);
    }

    [Fact]
    public void Watchdog_StaysQuiet_WhileEventsFlowOrTheCursorIsStill()
    {
        var source = new FakeInputSource();
        var clock = new FakeClock();
        var cursor = new FakeCursorProbe();
        using var monitor = new HookHealthMonitor(source, clock, new ListEventLog(), cursor, pollInterval: LongPoll);
        monitor.Start((in RawInput _) => false);

        for (var i = 0; i < 10; i++)
        {
            clock.Advance(HookHealthMonitor.DeadAfterMs);
            monitor.Poll();
        }

        for (var i = 0; i < 10; i++)
        {
            clock.Advance(HookHealthMonitor.DeadAfterMs);
            cursor.Y += 5;
            source.Deliver(RawInput.Move(cursor.X, cursor.Y, clock.MonotonicMs));
            monitor.Poll();
        }

        Assert.Equal(0, monitor.ReinstallCount);
        Assert.Equal(10, monitor.EventCount);
    }

    [Fact]
    public void NoCursorProbe_OnlyTheSourceSignalCounts()
    {
        var source = new FakeInputSource();
        var clock = new FakeClock();
        using var monitor = new HookHealthMonitor(source, clock, new ListEventLog(), pollInterval: LongPoll);
        monitor.Start((in RawInput _) => false);

        clock.Advance(10 * HookHealthMonitor.DeadAfterMs);
        monitor.Poll();

        Assert.Equal(0, monitor.ReinstallCount);
    }

    [Fact]
    public void FailedReinstall_IsLoggedAndRetriedWithBackoff()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var clock = new FakeClock();
        using var monitor = new HookHealthMonitor(source, clock, log, pollInterval: LongPoll);
        monitor.Start((in RawInput _) => false);

        source.ReportLost();
        source.FailNextStart = new InvalidOperationException("no desktop");
        monitor.Poll();
        Assert.Equal(0, monitor.ReinstallCount);
        Assert.True(log.Has(LogSources.Hook, "Hook reinstall failed"));

        monitor.Poll();
        Assert.Equal(1, source.StartCount);

        clock.Advance(5000);
        monitor.Poll();
        Assert.Equal(1, monitor.ReinstallCount);
        Assert.True(source.IsRunning);
    }

    [Fact]
    public void SystemEvents_AreLogged_AndResumeRequestsAReset()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var system = new FakeSystemEvents();
        var cursor = new FakeCursorProbe();
        var clock = new FakeClock();
        using var monitor = new HookHealthMonitor(source, clock, log, cursor, system, pollInterval: LongPoll);
        var resets = new List<string>();
        monitor.ResetRequested += (_, reason) => resets.Add(reason);
        monitor.Start((in RawInput _) => false);

        clock.Advance(3_600_000);
        system.Raise(SystemEventKind.Resumed);
        cursor.X = 50;
        monitor.Poll();

        Assert.Equal(["Resumed"], resets);
        Assert.Equal(0, monitor.ReinstallCount);
        Assert.True(log.Has(LogSources.Hook, "System event"));

        system.Raise(SystemEventKind.SessionLocked);
        Assert.Single(resets);
    }

    [Fact]
    public void ForegroundChanges_AreNotHookEvents_NothingLoggedNoReset()
    {
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var system = new FakeSystemEvents();
        using var monitor = new HookHealthMonitor(source, new FakeClock(), log, system: system, pollInterval: LongPoll);
        var resets = new List<string>();
        monitor.ResetRequested += (_, reason) => resets.Add(reason);
        monitor.Start((in RawInput _) => false);

        system.Raise(SystemEventKind.ForegroundChanged);

        Assert.Empty(resets);
        Assert.False(log.Has(LogSources.Hook, "System event"));
    }

    [Fact]
    public void HealthContributor_ReportsAliveSinceAndReinstalls()
    {
        var source = new FakeInputSource();
        var clock = new FakeClock();
        var registry = new HealthRegistry();
        using var monitor = new HookHealthMonitor(source, clock, new ListEventLog(), health: registry, pollInterval: LongPoll);

        Assert.Null(registry.Current().HookAliveSince);
        monitor.Start((in RawInput _) => false);
        source.Deliver(RawInput.Move(1, 1, 1));
        monitor.Poll();

        var snapshot = registry.Current();
        Assert.Equal(clock.UtcNow, snapshot.HookAliveSince);
        Assert.Equal(0, snapshot.HookReinstallCount);
        Assert.Equal(1, snapshot.EventsLastMinute);

        monitor.Stop();
        Assert.Equal(1, source.StopCount);
        Assert.False(source.IsRunning);
    }
}
