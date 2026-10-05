using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Pure merge policy for imported gestures (requirements F8): names clash case-insensitively,
/// conflicts are resolved per entry, and existing ids never change. No UI, no store.
/// </summary>
public static class GestureMerge
{
    public const string KeepBothSuffix = " (imported)";

    public static MergePlan Plan(IReadOnlyList<Gesture> existing, IReadOnlyList<Gesture> imported)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(imported);
        var byName = existing.ToDictionary(gesture => gesture.Name, StringComparer.OrdinalIgnoreCase);
        var entries = imported
            .Select(gesture => byName.TryGetValue(gesture.Name, out var clash)
                ? new MergeEntry(gesture, MergeKind.Conflict, clash)
                : new MergeEntry(gesture, MergeKind.Add, null))
            .ToList();
        return new MergePlan(existing, entries);
    }

    /// <summary>
    /// Applies the plan. <paramref name="choices"/> is keyed by the imported gesture's id; a conflict
    /// without a choice uses <paramref name="defaultChoice"/>. Returns the merged library in order:
    /// existing gestures first (replaced in place for TakeTheirs), then the additions.
    /// </summary>
    public static IReadOnlyList<Gesture> Apply(
        MergePlan plan,
        IReadOnlyDictionary<GestureId, MergeChoice> choices,
        MergeChoice defaultChoice = MergeChoice.KeepMine)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(choices);
        var result = plan.Existing.ToList();
        var names = new HashSet<string>(result.Select(gesture => gesture.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in plan.Entries)
        {
            if (entry.Kind == MergeKind.Add)
            {
                names.Add(entry.Imported.Name);
                result.Add(entry.Imported);
                continue;
            }

            var choice = choices.TryGetValue(entry.Imported.Id, out var chosen) ? chosen : defaultChoice;
            switch (choice)
            {
                case MergeChoice.TakeTheirs:
                    var existingId = entry.Existing!.Id;
                    var index = result.FindIndex(gesture => gesture.Id == existingId);
                    result[index] = entry.Imported with { Id = existingId };
                    break;
                case MergeChoice.KeepBoth:
                    var renamed = UniqueName(entry.Imported.Name + KeepBothSuffix, names);
                    names.Add(renamed);
                    result.Add(entry.Imported with { Name = renamed });
                    break;
                case MergeChoice.KeepMine:
                default:
                    break;
            }
        }

        return result;
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var n = 2; taken.Contains(candidate); n++)
        {
            candidate = name + " " + n;
        }

        return candidate;
    }
}
