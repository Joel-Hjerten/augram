using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// One run's merges, without I/O (README: the run): starting from this machine's items (or, for a
/// <see cref="SyncJoin.UseRemote"/> join, the newest other machine's), merge each other machine's file in turn
/// against its base and carry the result into the next. Per machine: fold its acknowledgement of one of our
/// published revisions into the base (minus the keys it did not take), leave alone the keys it holds, merge, keep
/// our conflicts pending (a held one stays as it was), then move the base to its file's content for every key
/// that is now settled. Conflicted and held keys keep their old base entry.
/// </summary>
internal static class SyncPlanner
{
    public static Plan Run(
        Guid self,
        IReadOnlyList<Gesture> gestures,
        MappingDocument mapping,
        IReadOnlyList<SyncFile> remotes,
        IReadOnlyDictionary<Guid, SyncMachineState> states,
        Func<Guid, SyncPublished?> published,
        SyncJoin? join,
        DateTimeOffset now)
    {
        var local = SyncItemSet.From(gestures, mapping);
        var current = local;
        var known = new Dictionary<Guid, SyncMachineState>(states);
        if (join == SyncJoin.UseRemote && remotes.Count > 0)
        {
            var adopted = remotes.MaxBy(file => file.WrittenAt)!;
            (gestures, mapping) = (adopted.Gestures, adopted.Mapping);
            current = SyncItemSet.From(gestures, mapping);
            known[adopted.MachineId] = new SyncMachineState(adopted.MachineId, adopted.MachineName) { Base = current.Contents() };
        }

        var repairs = new List<SyncRepair>();
        var merged = new List<SyncMachineState>();
        foreach (var remote in remotes)
        {
            var (result, state) = MergeOne(self, current, remote, known.GetValueOrDefault(remote.MachineId), published, now);
            (gestures, mapping, current) = (result.Gestures, result.Mapping, result.Items);
            repairs.AddRange(result.Repairs);
            merged.Add(state);
        }

        return new Plan(gestures, mapping, current, SyncCounts.Between(local, current), repairs, merged);
    }

    private static (SyncMergeResult Result, SyncMachineState State) MergeOne(
        Guid self,
        SyncItemSet current,
        SyncFile remote,
        SyncMachineState? state,
        Func<Guid, SyncPublished?> published,
        DateTimeOffset now)
    {
        state ??= new SyncMachineState(remote.MachineId, remote.MachineName);
        var remoteItems = SyncItemSet.From(remote.Gestures, remote.Mapping);
        var bases = new Dictionary<SyncItemKey, string>(state.Base);
        var acknowledged = state.AppliedAcknowledgement;
        var entry = remote.MergedFrom(self);
        if (entry is not null && entry.Revision != acknowledged)
        {
            if (published(entry.Revision) is { } ours)
            {
                Advance(bases, ours.Items, Keys(entry.Except));
            }

            acknowledged = entry.Revision;
        }

        var held = Keys(entry?.Pending ?? []);
        var result = ThreeWayMerge.Merge(bases, current, remoteItems, held);
        var previous = state.Conflicts.ToDictionary(conflict => conflict.Key);
        var conflicts = result.Conflicts
            .Select(conflict => conflict with
            {
                MachineId = remote.MachineId,
                MachineName = remote.MachineName,
                DetectedAt = previous.GetValueOrDefault(conflict.Key)?.DetectedAt ?? now,
            })
            .Concat(state.Conflicts.Where(conflict => held.Contains(conflict.Key)))
            .ToArray();

        var unsettled = conflicts.Select(conflict => conflict.Key).Concat(held).ToHashSet();
        foreach (var key in bases.Keys.Concat(remoteItems.Select(item => item.Key)).Distinct().ToArray())
        {
            if (unsettled.Contains(key))
            {
                continue;
            }

            if (remoteItems.Find(key) is { } item)
            {
                bases[key] = item.Content;
            }
            else
            {
                bases.Remove(key);
            }
        }

        return (result, state with
        {
            MachineName = remote.MachineName,
            Base = bases,
            MergedRevision = remote.Revision,
            AppliedAcknowledgement = acknowledged,
            Held = [.. held],
            Conflicts = conflicts,
            LastMerged = now,
        });
    }

    /// <summary>The other machine took our revision <paramref name="ours"/>: it is the shared base for every key it did not except.</summary>
    private static void Advance(Dictionary<SyncItemKey, string> bases, IReadOnlyDictionary<SyncItemKey, string> ours, HashSet<SyncItemKey> except)
    {
        foreach (var key in bases.Keys.Concat(ours.Keys).Distinct().Where(key => !except.Contains(key)).ToArray())
        {
            if (ours.TryGetValue(key, out var content))
            {
                bases[key] = content;
            }
            else
            {
                bases.Remove(key);
            }
        }
    }

    private static HashSet<SyncItemKey> Keys(IEnumerable<string> texts)
        => texts.Select(text => SyncItemKey.TryParse(text, out var key) ? key : (SyncItemKey?)null).OfType<SyncItemKey>().ToHashSet();

    /// <summary>The merged state of this run and the new state of every machine merged.</summary>
    internal sealed record Plan(
        IReadOnlyList<Gesture> Gestures,
        MappingDocument Mapping,
        SyncItemSet Items,
        SyncCounts Counts,
        IReadOnlyList<SyncRepair> Repairs,
        IReadOnlyList<SyncMachineState> States);
}
