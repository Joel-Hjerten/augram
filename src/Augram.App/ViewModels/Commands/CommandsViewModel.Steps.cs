using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The step half of <see cref="CommandsViewModel"/> (F5a step editing): every intent of the step list becomes one <c>UpdateCommand</c> on the selected command, one undo step each.</summary>
public sealed partial class CommandsViewModel
{
    public void Handle(StepListActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        Guard(() => Dispatch(e));
    }

    private void Dispatch(StepListActionEventArgs e)
    {
        switch (e.Action)
        {
            case StepListAction.Undo:
                _store.Undo();
                return;
            case StepListAction.Redo:
                _store.Redo();
                return;
        }

        if (SelectedCommandId is not { } id || _store.FindCommand(id) is not { } found)
        {
            Message = "Select a command first.";
            return;
        }

        var (group, command) = found;
        var steps = command.Steps.ToList();
        var step = e.Step is { } item && item.Index >= 0 && item.Index < steps.Count ? item : null;
        switch (e.Action)
        {
            case StepListAction.Select when step is not null:
                SelectedStepIndex = step.Index;
                break;
            case StepListAction.Add when e.Type is { } type:
                steps.Add(new CommandStep(type.CreateDefault(), _platform));
                Commit(group, command, steps, steps.Count - 1);
                break;
            case StepListAction.Edit when step is not null && e.Edited is { } edited:
                steps[step.Index] = steps[step.Index] with { Step = edited };
                Commit(group, command, steps, step.Index);
                break;
            case StepListAction.Duplicate when step is not null:
                steps.Insert(step.Index + 1, steps[step.Index]);
                Commit(group, command, steps, step.Index + 1);
                break;
            case StepListAction.Copy when step is not null:
                _clipboard.Step = steps[step.Index];
                Message = $"Copied step '{step.Summary}'. {CommandsKeymap.Current.Paste} pastes it at the bottom of a step list.";
                break;
            case StepListAction.Paste:
                if (_clipboard.Step is not { } copied)
                {
                    Message = "Nothing to paste: copy a step first.";
                    break;
                }

                steps.Add(copied);
                Commit(group, command, steps, steps.Count - 1);
                break;
            case StepListAction.Delete when step is not null:
                steps.RemoveAt(step.Index);
                Commit(group, command, steps, Math.Min(step.Index, steps.Count - 1));
                Message = $"Deleted step '{step.Summary}'. {CommandsKeymap.Current.Undo} undoes it.";
                break;
            case StepListAction.ToggleActive when step is not null:
                steps[step.Index] = steps[step.Index] with { IsActive = !steps[step.Index].IsActive };
                Commit(group, command, steps, step.Index);
                break;
            case StepListAction.Reorder when step is not null && e.TargetIndex >= 0 && e.TargetIndex < steps.Count && e.TargetIndex != step.Index:
                var moved = steps[step.Index];
                steps.RemoveAt(step.Index);
                steps.Insert(e.TargetIndex, moved);
                Commit(group, command, steps, e.TargetIndex);
                break;
        }
    }

    /// <summary>One store call per edit, then the step to leave expanded.</summary>
    private void Commit(AppGroup group, Command command, IReadOnlyList<CommandStep> steps, int select)
    {
        _store.UpdateCommand(group.Id, command with { Steps = steps });
        SelectedStepIndex = select;
    }
}
