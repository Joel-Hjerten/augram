using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>The outcome of <see cref="GestureMerge.Plan(IReadOnlyList{Gesture}, IReadOnlyList{Gesture})"/>: the untouched existing library plus one entry per imported gesture.</summary>
public sealed record MergePlan(IReadOnlyList<Gesture> Existing, IReadOnlyList<MergeEntry> Entries)
{
    /// <summary>Every entry that needs a <see cref="MergeChoice"/>: name conflicts and shape matches alike.</summary>
    public IEnumerable<MergeEntry> Conflicts => Entries.Where(entry => entry.Kind != MergeKind.Add);
}
