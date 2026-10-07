using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Asks how this machine joins a repo that already holds other machines' files (F8 sync, join): "Use the synced
/// settings on this machine" (the default) or "Merge". Null is Cancel: sync stays paused. Tests substitute a fake.
/// </summary>
public interface ISyncJoinPresenter
{
    Task<SyncJoin?> ChooseAsync(IReadOnlyList<SyncMachineSummary> machines);
}
