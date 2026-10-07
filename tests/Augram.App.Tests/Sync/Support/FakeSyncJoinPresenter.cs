using Augram.App.Sync;
using Augram.Core.Sync;

namespace Augram.App.Tests.Sync.Support;

/// <summary>Answers the join question with <see cref="Answer"/> (null = Cancel) at once and records what it was shown.</summary>
internal sealed class FakeSyncJoinPresenter : ISyncJoinPresenter
{
    private readonly object _gate = new();
    private readonly List<IReadOnlyList<SyncMachineSummary>> _asked = [];

    public SyncJoin? Answer { get; set; } = SyncJoin.UseRemote;

    public IReadOnlyList<IReadOnlyList<SyncMachineSummary>> Asked
    {
        get
        {
            lock (_gate)
            {
                return [.. _asked];
            }
        }
    }

    public Task<SyncJoin?> ChooseAsync(IReadOnlyList<SyncMachineSummary> machines)
    {
        lock (_gate)
        {
            _asked.Add(machines);
        }

        return Task.FromResult(Answer);
    }
}
