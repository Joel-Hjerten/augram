namespace Augram.Core.Sync;

/// <summary>
/// What the store-thread half of a sync did: the function <see cref="SyncCoordinator"/> hands to its
/// <c>onStoreThread</c> delegate returns one of these. <see cref="StoresMoved"/>: a store changed since the
/// worker took its snapshot, so nothing was applied and the coordinator merges again. <see cref="Error"/>: a
/// store refused the result (a bug: the result is validated first); nothing was applied.
/// </summary>
public sealed record SyncApplied(bool StoresMoved, string? Error = null)
{
    public static SyncApplied Done { get; } = new(false);

    public static SyncApplied Moved { get; } = new(true);

    public static SyncApplied Failed(string error) => new(false, error);
}
