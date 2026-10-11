using Augram.Core.Steps;

namespace Augram.App.Components.StepList;

/// <summary>One <see cref="StepListAction"/> with the step it applies to (null for Add, Paste, Undo, Redo and a Select that closes the open step), the type for Add and ChangeType, the new record for Edit and the destination for Reorder.</summary>
public sealed class StepListActionEventArgs : EventArgs
{
    public StepListActionEventArgs(StepListAction action, StepItem? step = null, IStepType? type = null, IStep? edited = null, int targetIndex = -1)
    {
        Action = action;
        Step = step;
        Type = type;
        Edited = edited;
        TargetIndex = targetIndex;
    }

    public StepListAction Action { get; }

    public StepItem? Step { get; }

    public IStepType? Type { get; }

    public IStep? Edited { get; }

    public int TargetIndex { get; }

    /// <summary>
    /// Where the step at <paramref name="index"/> sits after this Reorder: the moved step at <see cref="TargetIndex"/>, the
    /// ones it passed one place over, the rest where they were; -1 (no step) stays -1. The host keeps the open step open
    /// across a reorder with it, and a drag never opens the step it moved (Joel, 0.11.4).
    /// </summary>
    public int IndexAfterReorder(int index)
    {
        if (Action != StepListAction.Reorder || Step is not { } moved || index < 0)
        {
            return index;
        }

        var from = moved.Index;
        if (index == from)
        {
            return TargetIndex;
        }

        if (from < index && index <= TargetIndex)
        {
            return index - 1;
        }

        return TargetIndex <= index && index < from ? index + 1 : index;
    }
}
