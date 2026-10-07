using Augram.Core.Steps;

namespace Augram.App.Components.StepList;

/// <summary>One <see cref="StepListAction"/> with the step it applies to (null for Add, Paste, Undo, Redo), the type for Add, the new record for Edit and the destination for Reorder.</summary>
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
}
