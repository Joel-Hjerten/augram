using Augram.App.Hosting;
using Augram.App.Sync;
using Augram.Core.Sync;

namespace Augram.App.Tests.Sync.Support;

/// <summary>Answers the conflict dialog by applying <see cref="Choose"/> to every conflict (null = Cancel) and records what it was shown.</summary>
internal sealed class FakeSyncConflictPresenter : ISyncConflictPresenter
{
    public Func<SyncConflict, SyncChoice>? Choose { get; set; } = _ => SyncChoice.KeepMine;

    public List<IReadOnlyList<SyncConflict>> Shown { get; } = [];

    public Task<IReadOnlyList<SyncResolution>?> ResolveAsync(IReadOnlyList<SyncConflict> conflicts)
    {
        Shown.Add(conflicts);
        IReadOnlyList<SyncResolution>? answer = Choose is { } choose ? [.. conflicts.Select(conflict => new SyncResolution(conflict, choose(conflict)))] : null;
        return Task.FromResult(answer);
    }
}
