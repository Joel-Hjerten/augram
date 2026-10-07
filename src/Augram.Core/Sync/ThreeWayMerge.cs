namespace Augram.Core.Sync;

/// <summary>
/// The item-by-item three-way merge (F8 sync; README: the merge table). Pure: no I/O, no clock, no store.
/// Every key is decided by comparing contents against the base: the side that changed wins, both changed the
/// same way is no conflict, both changed differently (or one deleted what the other changed) is a conflict
/// whose result keeps the local state. The merged items are then rebuilt into a document and repaired where the
/// rules would refuse it (name clashes, A7, dangling references), always in favour of what was already here:
/// the incoming item is the one renamed or unbound.
/// </summary>
public static class ThreeWayMerge
{
    private enum Outcome
    {
        KeepLocal,
        TakeRemote,
        Conflict,
    }

    public static SyncMergeResult Merge(SyncItemSet @base, SyncItemSet local, SyncItemSet remote)
    {
        ArgumentNullException.ThrowIfNull(@base);
        return Merge(@base.Contents(), local, remote, held: null);
    }

    /// <param name="base">Content by key as of the last state both sides shared; a missing key was absent then.</param>
    /// <param name="local">This machine's items.</param>
    /// <param name="remote">The other machine's items.</param>
    /// <param name="held">Keys the other machine has a pending conflict on: kept as they are here, never reported.</param>
    public static SyncMergeResult Merge(
        IReadOnlyDictionary<SyncItemKey, string> @base,
        SyncItemSet local,
        SyncItemSet remote,
        IReadOnlySet<SyncItemKey>? held)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        var merged = new List<SyncItem>();
        var incoming = new HashSet<SyncItemKey>();
        var conflicts = new List<SyncConflict>();
        var keys = local.Select(item => item.Key).Concat(remote.Select(item => item.Key).Where(key => !local.Contains(key)));
        foreach (var key in keys)
        {
            var mine = local.Find(key);
            var theirs = remote.Find(key);
            var outcome = held is not null && held.Contains(key)
                ? Outcome.KeepLocal
                : Decide(@base.GetValueOrDefault(key), mine?.Content, theirs?.Content);

            switch (outcome)
            {
                case Outcome.TakeRemote when theirs is not null:
                    merged.Add(theirs);
                    incoming.Add(key);
                    break;
                case Outcome.TakeRemote:
                    break;
                case Outcome.Conflict:
                    conflicts.Add(new SyncConflict(key, (mine ?? theirs)!.Name, mine?.Content, theirs?.Content));
                    AddIfPresent(merged, mine);
                    break;
                default:
                    AddIfPresent(merged, mine);
                    break;
            }
        }

        var built = SyncDocumentBuilder.Build(merged, incoming, local);
        return new SyncMergeResult(built.Gestures, built.Mapping, conflicts, built.Repairs, SyncCounts.Between(local, built.Items))
        {
            Items = built.Items,
        };
    }

    /// <summary>The merge table for one key; null content is "absent on that side".</summary>
    private static Outcome Decide(string? @base, string? local, string? remote)
    {
        if (Same(local, remote))
        {
            return Outcome.KeepLocal;
        }

        if (Same(local, @base))
        {
            return Outcome.TakeRemote;
        }

        return Same(remote, @base) ? Outcome.KeepLocal : Outcome.Conflict;
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.Ordinal);

    private static void AddIfPresent(List<SyncItem> items, SyncItem? item)
    {
        if (item is not null)
        {
            items.Add(item);
        }
    }
}
