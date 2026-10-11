using Augram.Core.Gestures;
using Augram.Core.Recognition;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Pure merge policy for imported gestures (requirements F8): names clash as the rules compare them
/// (<see cref="GestureRules.NameComparer"/>, case-insensitive), a shape that scores as a duplicate of an
/// existing gesture is offered the same choices (A7: a re-import reuses the existing gesture rather than
/// adding a copy), conflicts are resolved per entry, and existing ids never change. No UI, no store.
/// </summary>
public static class GestureMerge
{
    public const string KeepBothSuffix = " (imported)";

    /// <summary>Classifies by name only: <see cref="MergeKind.Add"/> or <see cref="MergeKind.Conflict"/>.</summary>
    public static MergePlan Plan(IReadOnlyList<Gesture> existing, IReadOnlyList<Gesture> imported)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(imported);
        var byName = existing.ToDictionary(gesture => gesture.Name, GestureRules.NameComparer);
        var entries = imported
            .Select(gesture => byName.TryGetValue(gesture.Name, out var clash)
                ? new MergeEntry(gesture, MergeKind.Conflict, clash)
                : new MergeEntry(gesture, MergeKind.Add, null))
            .ToList();
        return new MergePlan(existing, entries);
    }

    /// <summary>
    /// Classifies by name, then scores each remaining addition's first sample against every existing
    /// gesture (inactive ones included) and turns a score at or above
    /// <see cref="ConfusionCheck.DuplicateCutOff"/> into a <see cref="MergeKind.SameShape"/> entry.
    /// </summary>
    public static MergePlan Plan(IReadOnlyList<Gesture> existing, IReadOnlyList<Gesture> imported, RecognitionOptions options, GestureMatcher? matcher = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var plan = Plan(existing, imported);
        if (existing.Count == 0)
        {
            return plan;
        }

        matcher ??= new GestureMatcher();
        var candidates = existing.Select(gesture => gesture.IsActive ? gesture : gesture with { IsActive = true }).ToList();
        var entries = plan.Entries.Select(entry => ShapeChecked(entry, existing, candidates, options, matcher)).ToList();
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
        => ApplyWithMap(plan, choices, defaultChoice).Gestures;

    /// <summary>As <see cref="Apply"/>, also returning imported id → final id for every imported gesture.</summary>
    public static MergeOutcome ApplyWithMap(
        MergePlan plan,
        IReadOnlyDictionary<GestureId, MergeChoice> choices,
        MergeChoice defaultChoice = MergeChoice.KeepMine)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(choices);
        var result = plan.Existing.ToList();
        var names = new HashSet<string>(result.Select(gesture => gesture.Name), GestureRules.NameComparer);
        var map = new Dictionary<GestureId, GestureId>();

        foreach (var entry in plan.Entries)
        {
            var imported = entry.Imported;
            if (entry.Kind == MergeKind.Add)
            {
                names.Add(imported.Name);
                result.Add(imported);
                map[imported.Id] = imported.Id;
                continue;
            }

            var existingId = entry.Existing!.Id;
            var choice = choices.TryGetValue(imported.Id, out var chosen) ? chosen : defaultChoice;
            switch (choice)
            {
                case MergeChoice.TakeTheirs:
                    var index = result.FindIndex(gesture => gesture.Id == existingId);
                    result[index] = imported with { Id = existingId };
                    map[imported.Id] = existingId;
                    break;
                case MergeChoice.KeepBoth:
                    var wanted = entry.Kind == MergeKind.Conflict ? imported.Name + KeepBothSuffix : imported.Name;
                    var renamed = UniqueName(wanted, names);
                    names.Add(renamed);
                    result.Add(imported with { Name = renamed });
                    map[imported.Id] = imported.Id;
                    break;
                case MergeChoice.KeepMine:
                default:
                    map[imported.Id] = existingId;
                    break;
            }
        }

        return new MergeOutcome(result, map);
    }

    private static MergeEntry ShapeChecked(MergeEntry entry, IReadOnlyList<Gesture> existing, IReadOnlyList<Gesture> candidates, RecognitionOptions options, GestureMatcher matcher)
    {
        if (entry.Kind != MergeKind.Add || entry.Imported.Samples.Count == 0)
        {
            return entry;
        }

        var best = matcher.Rank(entry.Imported.Samples[0], candidates, options).FirstOrDefault();
        if (best is null || best.Score < ConfusionCheck.DuplicateCutOff)
        {
            return entry;
        }

        var twin = existing.First(gesture => gesture.Id == best.GestureId);
        return new MergeEntry(entry.Imported, MergeKind.SameShape, twin, best.Score);
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
