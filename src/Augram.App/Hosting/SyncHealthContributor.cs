using Augram.Core.Diagnostics;
using Augram.Core.Sync;

namespace Augram.App.Hosting;

/// <summary>
/// Contributes the last sync (F8) to the health summary (N4): its status and when it finished, read from
/// <see cref="SyncService.LastReport"/>; a pause for a newer Augram says whose ("Paused (Mac uses a newer Augram)").
/// </summary>
public sealed class SyncHealthContributor : IDisposable
{
    private readonly IDisposable _registration;

    public SyncHealthContributor(HealthRegistry registry, SyncService service)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(service);
        _registration = registry.Register(snapshot => service.LastReport is { } report
            ? snapshot with { LastSyncOutcome = Outcome(report), LastSyncAt = report.When }
            : snapshot);
    }

    public void Dispose() => _registration.Dispose();

    public static string Outcome(SyncReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Status == SyncStatus.NeedsUpdate ? $"Paused ({SyncNewerMachine.Summary(report.NewerMachines)})" : report.Status.ToString();
    }
}
