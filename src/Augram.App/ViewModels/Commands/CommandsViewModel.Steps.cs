using Augram.App.Components.ListEditing;
using Augram.App.Components.StepList;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The step half of <see cref="CommandsViewModel"/> (F5a step editing): every intent of the step list becomes one
/// <c>UpdateCommand</c> on the selected command, one undo step each. The list edited is this platform's (F8): the
/// original where it was authored, the own version where it has one, and otherwise the converted original, so the first
/// edit on the other platform makes its own steps from what was running there (<see cref="Command.WithStepsFor"/>). A step
/// added (new, duplicated or pasted) or changed to another type is first offered to Core's <see cref="StepOffer"/>, as "New
/// step…" and "Change to…" grey a type by.
/// </summary>
public sealed partial class CommandsViewModel
{
    public void Handle(StepListActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        if (!Guard(() => Dispatch(e)) && e.Action == StepListAction.Edit && SelectedCommandId is not null)
        {
            // A refused edit (a rule on the Remap output, plan 0002) changed nothing stored: hand the list the stored steps
            // again, so the form that emitted it is rebuilt on them while the message line says why.
            Steps = [.. Steps];
        }
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
        var steps = EditableSteps(command).ToList();
        var step = e.Step is { } item && item.Index >= 0 && item.Index < steps.Count ? item : null;
        switch (e.Action)
        {
            case StepListAction.Select when step is not null:
                SelectedStepIndex = step.Index;
                break;
            case StepListAction.Select when e.Step is null:
                // A click on the open step's header, or a drag starting on it, closes it (Joel, 0.11.4).
                SelectedStepIndex = -1;
                break;
            case StepListAction.Add when e.Type is { } type:
                var added = NewStep(type, command);
                if (Offered(command, added))
                {
                    steps.Add(new CommandStep(added, _platform));
                    Commit(group, command, steps, steps.Count - 1);
                }

                break;
            case StepListAction.ChangeType when step is not null && e.Type is { } type:
                // A default step of the new type, made here, in the step's place and with its on/off state; the old type's
                // settings do not carry across. One store call, so one undo gives the old step back with all of them.
                var changed = new CommandStep(NewStep(type, command), _platform, steps[step.Index].IsActive);
                if (Offered(command, changed.Step, replacing: step.Index))
                {
                    steps[step.Index] = changed;
                    Commit(group, command, steps, step.Index);
                    Message ??= $"Changed step '{step.Summary}' to {type.DisplayName}. {EditKeymap.Current.Undo} undoes it.";
                }

                break;
            case StepListAction.Edit when step is not null && e.Edited is { } edited:
                steps[step.Index] = steps[step.Index] with { Step = edited };
                Commit(group, command, steps, step.Index);
                break;
            case StepListAction.Duplicate when step is not null:
                if (Offered(command, steps[step.Index].Step))
                {
                    steps.Insert(step.Index + 1, steps[step.Index]);
                    Commit(group, command, steps, step.Index + 1);
                }

                break;
            case StepListAction.Copy when step is not null:
                _clipboard.Step = steps[step.Index];
                Message = $"Copied step '{step.Summary}'. {EditKeymap.Current.Paste} pastes it at the bottom of a step list.";
                break;
            case StepListAction.Paste:
                if (_clipboard.Step is not { } copied)
                {
                    Message = "Nothing to paste: copy a step first.";
                    break;
                }

                if (Offered(command, copied.Step))
                {
                    steps.Add(copied);
                    Commit(group, command, steps, steps.Count - 1);
                }

                break;
            case StepListAction.Delete when step is not null:
                steps.RemoveAt(step.Index);
                Commit(group, command, steps, Math.Min(step.Index, steps.Count - 1));
                Message = $"Deleted step '{step.Summary}'. {EditKeymap.Current.Undo} undoes it.";
                break;
            case StepListAction.ToggleActive when step is not null:
                // The active box opens and closes nothing (Joel, 0.11.4).
                steps[step.Index] = steps[step.Index] with { IsActive = !steps[step.Index].IsActive };
                Commit(group, command, steps, SelectedStepIndex);
                break;
            case StepListAction.Reorder when step is not null && e.TargetIndex >= 0 && e.TargetIndex < steps.Count && e.TargetIndex != step.Index:
                var moved = steps[step.Index];
                steps.RemoveAt(step.Index);
                steps.Insert(e.TargetIndex, moved);
                Commit(group, command, steps, e.IndexAfterReorder(SelectedStepIndex));
                break;
        }
    }

    /// <summary>
    /// True when <paramref name="step"/> (a new one, a duplicate, a paste) may join the command's steps here, or, with
    /// <paramref name="replacing"/>, take the place of the step at that index ("Change to…"; Core's <see cref="StepOffer"/>, the
    /// rule "New step…" and "Change to…" grey a type by); else the message line says why in the picker's words ("A Remap step is
    /// a command's only step.") and nothing is stored.
    /// </summary>
    private bool Offered(Command command, IStep step, int? replacing = null)
    {
        var drafted = DraftedTrigger(command.Id);
        var refusal = replacing is { } index
            ? StepOffer.CheckChange(step, command, index, _platform, drafted)
            : StepOffer.Check(step, command, _platform, drafted);
        if (refusal is null)
        {
            return true;
        }

        Message = refusal;
        return false;
    }

    /// <summary>One store call per edit, then the step to leave expanded.</summary>
    private void Commit(AppGroup group, Command command, IReadOnlyList<CommandStep> steps, int select)
    {
        var forked = command.Origin is { } origin && origin != _platform && command.OwnVersion?.Platform != _platform;
        _store.UpdateCommand(group.Id, command.WithStepsFor(_platform, steps, DateTimeOffset.UtcNow));
        SelectedStepIndex = select;
        if (forked)
        {
            Message = $"'{command.Name}' now has its own steps here; the original keeps running where it was authored. {EditKeymap.Current.Undo} undoes it.";
        }
    }

    /// <summary>The step list this platform edits: its own version, the original where it was authored, else the converted original.</summary>
    private IReadOnlyList<CommandStep> EditableSteps(Command command)
        => command.Origin is { } origin && origin != _platform && command.OwnVersion?.Platform != _platform
            ? command.ConvertedFor(_platform)
            : command.StepsFor(_platform);
}
