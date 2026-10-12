using System.Collections.Immutable;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// Where a step's index goes when the list changes around it (F5a, Joel 2026-10-12: several steps may be open, and every step
/// action opens and closes nothing else): the one home of the shifting rules for the selected step and the set of open steps
/// (<see cref="CommandsViewModel.SelectedStepIndex"/>, <see cref="CommandsViewModel.OpenStepIndexes"/>). An index of -1 is
/// no step and stays -1; a set drops what has no step any more. Pure; <see cref="StepEditor"/> applies it per intent.
/// </summary>
internal static class StepIndexShift
{
    /// <summary>A step was inserted at <paramref name="at"/>: the steps from there on move one down.</summary>
    public static int AfterInsert(int index, int at) => index >= 0 && index >= at ? index + 1 : index;

    /// <summary>The step at <paramref name="removed"/> was deleted: it is gone (-1), the steps below move one up.</summary>
    public static int AfterRemove(int index, int removed) => index == removed ? -1 : index > removed ? index - 1 : index;

    /// <summary>
    /// The step at <paramref name="from"/> moved to <paramref name="to"/>: it is at <paramref name="to"/>, the ones it passed
    /// one place over, the rest where they were.
    /// </summary>
    public static int AfterMove(int index, int from, int to)
    {
        if (index < 0)
        {
            return index;
        }

        if (index == from)
        {
            return to;
        }

        if (from < index && index <= to)
        {
            return index - 1;
        }

        return to <= index && index < from ? index + 1 : index;
    }

    /// <summary>The open steps after an insert at <paramref name="at"/>, the inserted step open (a new, duplicated or pasted step opens).</summary>
    public static ImmutableSortedSet<int> AfterInsert(IReadOnlySet<int> open, int at) => Map(open, index => AfterInsert(index, at)).Add(at);

    public static ImmutableSortedSet<int> AfterRemove(IReadOnlySet<int> open, int removed) => Map(open, index => AfterRemove(index, removed));

    /// <summary>The open steps after a move: open state travels with each step.</summary>
    public static ImmutableSortedSet<int> AfterMove(IReadOnlySet<int> open, int from, int to) => Map(open, index => AfterMove(index, from, to));

    /// <summary>Opens the step at <paramref name="index"/> when it is closed, closes it when it is open; the others stay as they are.</summary>
    public static ImmutableSortedSet<int> Toggle(IReadOnlySet<int> open, int index)
    {
        var set = open.ToImmutableSortedSet();
        return set.Contains(index) ? set.Remove(index) : set.Add(index);
    }

    /// <summary>The open steps that still exist in a list of <paramref name="count"/> steps (after an undo or a redo).</summary>
    public static ImmutableSortedSet<int> Clamp(IReadOnlySet<int> open, int count) => Map(open, index => index < count ? index : -1);

    private static ImmutableSortedSet<int> Map(IReadOnlySet<int> open, Func<int, int> shift)
        => open.Select(shift).Where(index => index >= 0).ToImmutableSortedSet();
}
