using Augram.Core.Diagnostics;

namespace Augram.App.Hosting;

/// <summary>Contributes the process-level health fields (N4): uptime and working set.</summary>
public sealed class AppHealthContributor : IDisposable
{
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private readonly IDisposable _registration;

    public AppHealthContributor(HealthRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registration = registry.Register(snapshot => snapshot with
        {
            UptimeSeconds = (DateTimeOffset.UtcNow - _started).TotalSeconds,
            WorkingSetBytes = Environment.WorkingSet,
        });
    }

    public void Dispose() => _registration.Dispose();
}
