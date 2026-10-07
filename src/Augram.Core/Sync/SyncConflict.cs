namespace Augram.Core.Sync;

/// <summary>
/// An item changed on both machines in different ways since they last agreed, or deleted on one and changed
/// on the other (F8 sync). Until it is resolved (<see cref="SyncCoordinator.Resolve"/>: keep mine, take
/// theirs, keep both) this machine keeps its own version and the other machine leaves the item alone.
/// A null content is "deleted on that side". <see cref="MachineId"/> and <see cref="MachineName"/> name the
/// other machine; <see cref="ThreeWayMerge"/> leaves them empty and the coordinator fills them in.
/// </summary>
public sealed record SyncConflict(SyncItemKey Key, string Name, string? LocalContent, string? RemoteContent)
{
    public SyncItemKind Kind => Key.Kind;

    public Guid MachineId { get; init; }

    public string MachineName { get; init; } = string.Empty;

    /// <summary>When it was first found; kept while the conflict stays pending.</summary>
    public DateTimeOffset DetectedAt { get; init; }

    public bool DeletedHere => LocalContent is null;

    public bool DeletedThere => RemoteContent is null;
}
