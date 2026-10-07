using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.Core.Tests.Diagnostics;

/// <summary>Contributors compose a <see cref="HealthSnapshot"/> at read time; registrations are removable.</summary>
public sealed class HealthRegistryTests
{
    [Fact]
    public void NoContributors_EveryFieldIsNull()
    {
        IHealthSource source = new HealthRegistry();

        var snapshot = source.Current();

        Assert.Equal(HealthSnapshot.Empty, snapshot);
        Assert.Null(snapshot.HookAliveSince);
        Assert.Null(snapshot.WorkingSetBytes);
    }

    [Fact]
    public void Contributors_FillTheirOwnFields()
    {
        var registry = new HealthRegistry();
        var aliveSince = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(2));
        registry.Register(s => s with { HookAliveSince = aliveSince, HookReinstallCount = 1 });
        registry.Register(s => s with { LastStrokeLatencyMs = 3.5, LastActivationOutcome = "SetForegroundWindow ok" });

        var snapshot = registry.Current();

        Assert.Equal(aliveSince, snapshot.HookAliveSince);
        Assert.Equal(1, snapshot.HookReinstallCount);
        Assert.Equal(3.5, snapshot.LastStrokeLatencyMs);
        Assert.Equal("SetForegroundWindow ok", snapshot.LastActivationOutcome);
        Assert.Null(snapshot.EventsLastMinute);
        Assert.Equal(2, registry.ContributorCount);
    }

    [Fact]
    public void TheSyncContributorFillsTheLastSyncFields()
    {
        var registry = new HealthRegistry();
        var finished = new DateTimeOffset(2026, 10, 7, 14, 32, 0, TimeSpan.Zero);
        registry.Register(s => s with { LastSyncOutcome = "UpToDate", LastSyncAt = finished });

        var snapshot = registry.Current();

        Assert.Equal("UpToDate", snapshot.LastSyncOutcome);
        Assert.Equal(finished, snapshot.LastSyncAt);
        Assert.Null(HealthSnapshot.Empty.LastSyncOutcome);
        Assert.Null(HealthSnapshot.Empty.LastSyncAt);
    }

    [Fact]
    public void Contributors_ReadLiveStateOnEachCall()
    {
        var registry = new HealthRegistry();
        var events = 0;
        registry.Register(s => s with { EventsLastMinute = events });

        Assert.Equal(0, registry.Current().EventsLastMinute);
        events = 42;
        Assert.Equal(42, registry.Current().EventsLastMinute);
    }

    [Fact]
    public void LaterRegistration_WinsOnASharedField()
    {
        var registry = new HealthRegistry();
        registry.Register(s => s with { UptimeSeconds = 1 });
        registry.Register(s => s with { UptimeSeconds = 2 });

        Assert.Equal(2, registry.Current().UptimeSeconds);
    }

    [Fact]
    public void DisposingTheRegistration_RemovesTheContributor()
    {
        var registry = new HealthRegistry();
        var hook = registry.Register(s => s with { HookReinstallCount = 3 });
        registry.Register(s => s with { WorkingSetBytes = 1024 });

        hook.Dispose();
        hook.Dispose();

        var snapshot = registry.Current();
        Assert.Null(snapshot.HookReinstallCount);
        Assert.Equal(1024, snapshot.WorkingSetBytes);
        Assert.Equal(1, registry.ContributorCount);
    }

    [Fact]
    public void NullContributor_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthRegistry().Register(null!));
    }
}
