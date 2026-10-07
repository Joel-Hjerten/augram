using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// The document side of resolving one <see cref="SyncConflict"/> (README: conflicts), pure: what the gestures and
/// mapping become under the user's choice, repaired and validated like a merge result. Keep mine changes nothing.
/// Take theirs puts the other machine's version in place of ours (or deletes ours when theirs is a deletion).
/// Keep both adds theirs beside ours with a new id; it applies to gestures and commands that exist on both sides,
/// and <see cref="Effective"/> turns it into the nearest choice otherwise, with a note saying so.
/// </summary>
internal static class ConflictResolution
{
    /// <summary>The choice that will actually be applied, and the note to show when it differs from the one asked for.</summary>
    public static (SyncChoice Choice, string? Note) Effective(SyncConflict conflict, SyncChoice choice)
    {
        if (choice != SyncChoice.KeepBoth)
        {
            return (choice, null);
        }

        if (conflict.DeletedThere)
        {
            return (SyncChoice.KeepMine, $"'{conflict.Name}' was deleted on {conflict.MachineName}, so keeping both keeps this machine's.");
        }

        if (conflict.DeletedHere)
        {
            return (SyncChoice.TakeTheirs, $"'{conflict.Name}' was deleted here, so keeping both takes {conflict.MachineName}'s.");
        }

        return conflict.Kind is SyncItemKind.Gesture or SyncItemKind.Command
            ? (choice, null)
            : (SyncChoice.TakeTheirs, $"Keep both is for gestures and commands; '{conflict.Name}' took {conflict.MachineName}'s version.");
    }

    /// <summary>The resolved document; <paramref name="choice"/> is an effective one (never Keep both where it does not apply).</summary>
    public static SyncMergeResult Apply(SyncItemSet local, SyncConflict conflict, SyncChoice choice, StepRegistry steps)
    {
        var items = local.ToList();
        var incoming = new HashSet<SyncItemKey>();
        int index = items.FindIndex(item => item.Key == conflict.Key);
        if (choice == SyncChoice.TakeTheirs)
        {
            if (index >= 0)
            {
                items.RemoveAt(index);
            }

            if (conflict.RemoteContent is { } content)
            {
                items.Insert(index >= 0 ? index : items.Count, SyncItem.Parse(conflict.Key, content, steps));
                incoming.Add(conflict.Key);
            }
        }
        else if (choice == SyncChoice.KeepBoth && conflict.RemoteContent is { } content)
        {
            var copy = Copy(SyncItem.Parse(conflict.Key, content, steps));
            items.Add(copy);
            incoming.Add(copy.Key);
        }

        var built = SyncDocumentBuilder.Build(items, incoming, local);
        return new SyncMergeResult(built.Gestures, built.Mapping, [], built.Repairs, SyncCounts.Between(local, built.Items))
        {
            Items = built.Items,
        };
    }

    private static SyncItem Copy(SyncItem item) => item switch
    {
        SyncItem.GestureItem gesture => new SyncItem.GestureItem(gesture.Gesture with { Id = GestureId.New() }),
        SyncItem.CommandItem command => new SyncItem.CommandItem(command.GroupId, command.Command with { Id = CommandId.New() }),
        _ => throw new InvalidOperationException($"Keep both does not apply to a {item.Kind}."),
    };
}
