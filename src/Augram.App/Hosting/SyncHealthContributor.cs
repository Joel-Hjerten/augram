using Augram.Core.Diagnostics;

namespace Augram.App.Hosting;

/// <summary>Contributes the last sync (F8) to the health summary (N4): its status and when it finished, read from <see cref="SyncService.LastReport"/>.</summary>
public sealed class SyncHealthContributor : IDisposable
{
    private readonly IDisposable _registration;

    public SyncHealthContributor(HealthRegistry registry, SyncService service)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(service);
        _registration = registry.Register(snapshot => service.LastReport is { } report
            ? snapshot with { LastSyncOutcome = report.Status.ToString(), LastSyncAt = report.When }
            : snapshot);
    }

    public void Dispose() => _registration.Dispose();
}
