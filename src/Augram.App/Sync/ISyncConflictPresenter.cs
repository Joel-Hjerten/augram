using Augram.App.Hosting;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Shows the pending sync conflicts with Keep mine / Take theirs / Keep both per conflict (F8 sync) and returns the
/// choices, or null for Cancel (everything stays pending). Tests substitute a fake.
/// </summary>
public interface ISyncConflictPresenter
{
    Task<IReadOnlyList<SyncResolution>?> ResolveAsync(IReadOnlyList<SyncConflict> conflicts);
}
