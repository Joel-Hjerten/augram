using Augram.App.Components.ListEditing;
using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The step list's intents of <see cref="CommandsViewModel"/> (F5a step editing): every intent of the step list, a step
/// form's edit included, becomes one <c>UpdateCommand</c> on the selected command, one undo step each. The list edited is this
/// platform's (F8): the original where it was authored, the own version where it has one, and otherwise the converted
/// original, so the first edit on the other platform makes its own steps from what was running there
/// (<see cref="Command.WithStepsFor"/>; <see cref="EditableSteps"/>). A step added (new, duplicated or pasted) or changed to
/// another type is first offered to Core's <see cref="StepOffer"/>, as "New step…" and "Change to…" grey a type by, against
/// the header's trigger (<see cref="TriggerEditor.DraftedTrigger"/>, a draft included). It owns no state: the expanded step
/// is the view model's <see cref="CommandsViewModel.SelectedStepIndex"/>.
/// </summary>
internal sealed class StepEditor
{
    private readonly MappingStore _store;
    private readonly CommandClipboard _clipboard;
    private readonly HostPlatform _platform;
    private readonly TriggerEditor _trigger;
    private readonly ICommandsHost _host;

    public StepEditor(MappingStore store, CommandClipboard clipboard, HostPlatform platform, TriggerEditor trigger, ICommandsHost host)
    {
        _store = store;
        _clipboard = clipboard;
        _platform = platform;
        _trigger = trigger;
        _host = host;
    }

    /// <summary>The step list this platform edits: its own version, the original where it was authored, else the converted original.</summary>
    public static IReadOnlyList<CommandStep> EditableSteps(Command command, HostPlatform platform)
        => command.Origin is { } origin && origin != platform && command.OwnVersion?.Platform != platform
            ? command.ConvertedFor(platform)
            : command.StepsFor(platform);

    /// <summary>One step list intent; the view model runs it inside <c>Guard</c>.</summary>
    public void Dispatch(StepListActionEventArgs e)
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

        if (_host.SelectedCommandId is not { } id || _store.FindCommand(id) is not { } found)
        {
            _host.Message = "Select a command first.";
            return;
        }

        var (group, command) = found;
        var steps = EditableSteps(command, _platform).ToList();
        var step = e.Step is { } item && item.Index >= 0 && item.Index < steps.Count ? item : null;
        switch (e.Action)
        {
            case StepListAction.Select when step is not null:
                _host.SelectedStepIndex = step.Index;
                break;
            case StepListAction.Select when e.Step is null:
                // A click on the open step's header, or a drag starting on it, closes it (Joel, 0.11.4).
                _host.SelectedStepIndex = -1;
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
                    _host.Message ??= $"Changed step '{step.Summary}' to {type.DisplayName}. {EditKeymap.Current.Undo} undoes it.";
                }

                break;
            case StepListAction.Edit when step is not null && e.Edited is { } edited:
                // An edit is made in this platform's terms (the form shows its key names), so the step is authored here:
                // a Windows "Win+X" kept unconverted in a Mac version, edited to Opt+Shift+X, still read and ran as Windows.
                steps[step.Index] = steps[step.Index] with { Step = edited, AuthoredOn = _platform };
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
                _host.Message = $"Copied step '{step.Summary}'. {EditKeymap.Current.Paste} pastes it at the bottom of a step list.";
                break;
            case StepListAction.Paste:
                if (_clipboard.Step is not { } copied)
                {
                    _host.Message = "Nothing to paste: copy a step first.";
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
                _host.Message = $"Deleted step '{step.Summary}'. {EditKeymap.Current.Undo} undoes it.";
                break;
            case StepListAction.ToggleActive when step is not null:
                // The active box opens and closes nothing (Joel, 0.11.4).
                steps[step.Index] = steps[step.Index] with { IsActive = !steps[step.Index].IsActive };
                Commit(group, command, steps, _host.SelectedStepIndex);
                break;
            case StepListAction.Reorder when step is not null && e.TargetIndex >= 0 && e.TargetIndex < steps.Count && e.TargetIndex != step.Index:
                var moved = steps[step.Index];
                steps.RemoveAt(step.Index);
                steps.Insert(e.TargetIndex, moved);
                Commit(group, command, steps, e.IndexAfterReorder(_host.SelectedStepIndex));
                break;
        }
    }

    /// <summary>
    /// A new step of <paramref name="type"/> for the command, as Core makes it (<see cref="StepOffer.NewStep"/>: a Remap step's
    /// output fitted to the command's input here, or a key output on a button trigger, the header's, a draft included).
    /// </summary>
    private IStep NewStep(IStepType type, Command command) => StepOffer.NewStep(type, command, _platform, _trigger.DraftedTrigger(command.Id));

    /// <summary>
    /// True when <paramref name="step"/> (a new one, a duplicate, a paste) may join the command's steps here, or, with
    /// <paramref name="replacing"/>, take the place of the step at that index ("Change to…"; Core's <see cref="StepOffer"/>, the
    /// rule "New step…" and "Change to…" grey a type by); else the message line says why in the picker's words ("A Remap step is
    /// a command's only step.") and nothing is stored.
    /// </summary>
    private bool Offered(Command command, IStep step, int? replacing = null)
    {
        var drafted = _trigger.DraftedTrigger(command.Id);
        var refusal = replacing is { } index
            ? StepOffer.CheckChange(step, command, index, _platform, drafted)
            : StepOffer.Check(step, command, _platform, drafted);
        if (refusal is null)
        {
            return true;
        }

        _host.Message = refusal;
        return false;
    }

    /// <summary>One store call per edit, then the step to leave expanded.</summary>
    private void Commit(AppGroup group, Command command, IReadOnlyList<CommandStep> steps, int select)
    {
        var forked = command.Origin is { } origin && origin != _platform && command.OwnVersion?.Platform != _platform;
        _store.UpdateCommand(group.Id, command.WithStepsFor(_platform, steps, DateTimeOffset.UtcNow));
        _host.SelectedStepIndex = select;
        if (forked)
        {
            _host.Message = $"'{command.Name}' now has its own steps here; the original keeps running where it was authored. {EditKeymap.Current.Undo} undoes it.";
        }
    }
}
