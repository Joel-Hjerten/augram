using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// The outcome of <see cref="ThreeWayMerge.Merge(SyncItemSet, SyncItemSet, SyncItemSet)"/>: the merged gestures
/// and mapping, already repaired and validated by <see cref="GestureRules"/> and <see cref="MappingRules"/> (safe
/// to hand to <c>ReplaceAll</c>), the conflicts (where the result kept the local version), the repairs, and what
/// changed relative to the local side.
/// </summary>
public sealed record SyncMergeResult(
    IReadOnlyList<Gesture> Gestures,
    MappingDocument Mapping,
    IReadOnlyList<SyncConflict> Conflicts,
    IReadOnlyList<SyncRepair> Repairs,
    SyncCounts Counts)
{
    /// <summary>The result as items, for the next merge in a sequence and for the base it leaves behind.</summary>
    public SyncItemSet Items { get; init; } = SyncItemSet.Empty;
}
