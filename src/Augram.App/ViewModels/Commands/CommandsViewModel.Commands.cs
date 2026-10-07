using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The command half of <see cref="CommandsViewModel"/> (F5a, F3): new and paste into the selected section
/// (its group, and on the Global tab its category), copy, delete with confirmation, the category, and the
/// trigger (wheel, none, or the Select Gesture picker).
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>Where New command and Paste go: the section acted on, else the selected one; Uncategorized on the Global tab when there is neither.</summary>
    private SectionId? TargetOf(SectionItem? section)
        => section?.Id ?? SelectedSectionId ?? (Scope == CommandsScope.Global ? SectionId.Uncategorized : null);

    /// <summary>"New command N" in the section, unbound and empty, selected and handed to the tree for renaming.</summary>
    private void NewCommand(SectionId? target)
    {
        if (RequireTarget(target) is not { } section)
        {
            return;
        }

        var group = RequireGroup(section.GroupId);
        _expanded.Add(section);
        var name = FreeNames.Next("New command", group.Commands.Select(command => command.Name));
        var stored = _store.AddCommand(group.Id, new Command(CommandId.New(), name, Trigger.None, IsActive: true, Steps: [], CategoryId: section.CategoryId));
        Select(section, stored.Id);
        ProjectSelection();
        RenameRequested?.Invoke(this, stored.Id);
    }

    private void CopyCommand(CommandItem command)
    {
        _clipboard.Command = RequireCommand(command.Id).Command;
        Message = $"Copied '{command.Name}'. Paste it into a group or a category with {CommandsKeymap.Current.Paste}.";
    }

    /// <summary>A copy with a fresh id and a free name; when the group already uses the trigger (A7) it is pasted unbound rather than refused.</summary>
    private void PasteCommand(SectionId? target)
    {
        if (_clipboard.Command is not { } source)
        {
            Message = "Nothing to paste: copy a command first.";
            return;
        }

        if (RequireTarget(target) is not { } section)
        {
            return;
        }

        var group = RequireGroup(section.GroupId);
        _expanded.Add(section);
        var name = FreeNames.CopyOf(source.Name, group.Commands.Select(command => command.Name));
        var copy = source with { Id = CommandId.New(), Name = name, CategoryId = PastedCategory(section, group, source) };
        Command stored;
        try
        {
            stored = _store.AddCommand(group.Id, copy);
        }
        catch (MappingValidationException) when (copy.Trigger.IsBound)
        {
            stored = _store.AddCommand(group.Id, copy with { Trigger = Trigger.None });
            Message = $"Pasted '{stored.Name}' into '{group.Name}' without its trigger: that trigger is already used there.";
        }

        Select(section, stored.Id);
        ProjectSelection();
    }

    /// <summary>
    /// A Global section decides the category (Uncategorized included); an app group keeps the copied
    /// command's category when it has one of the same name, else the copy is uncategorized there.
    /// </summary>
    private CategoryId? PastedCategory(SectionId target, AppGroup group, Command source)
    {
        if (group.IsGlobal)
        {
            return target.CategoryId;
        }

        var name = source.CategoryId is { } id ? _store.FindCommand(source.Id)?.Group.FindCategory(id)?.Name : null;
        return name is null ? null : group.Categories.FirstOrDefault(category => MappingRules.NameComparer.Equals(category.Name, name))?.Id;
    }

    private async Task DeleteCommandAsync(CommandItem command)
    {
        if (!await _confirm.ConfirmAsync("Delete command", $"Delete command '{command.Name}'?", "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var removed = _store.RemoveCommand(command.Id);
            Message = $"Deleted '{removed.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }

    /// <summary>The header's Category dropdown; the section it lands in is expanded so the selected row stays in sight.</summary>
    private void SetCategory(CommandItem command, CategoryId? category)
    {
        var (group, stored) = RequireCommand(command.Id);
        var updated = _store.UpdateCommand(group.Id, stored with { CategoryId = category });
        if (_expanded.Add(CommandSections.SectionOf(Scope, RequireGroup(group.Id), updated)))
        {
            Project();
        }
    }

    private void SetTriggerKind(CommandItem command, TriggerKind kind)
    {
        switch (kind)
        {
            case TriggerKind.None:
                UpdateCommand(command.Id, stored => stored with { Trigger = Trigger.None });
                break;
            case TriggerKind.WheelUp:
                UpdateCommand(command.Id, stored => stored with { Trigger = Trigger.ForWheel(WheelDirection.Up) });
                break;
            case TriggerKind.WheelDown:
                UpdateCommand(command.Id, stored => stored with { Trigger = Trigger.ForWheel(WheelDirection.Down) });
                break;
            case TriggerKind.Gesture:
                _ = PickGestureAsync(command);
                break;
        }
    }

    private async Task PickGestureAsync(CommandItem command)
    {
        var current = RequireCommand(command.Id).Command.Trigger is Trigger.GestureTrigger gesture ? gesture.GestureId : (GestureId?)null;
        var result = await _picker.PickAsync(current).ConfigureAwait(true);
        Guard(() =>
        {
            switch (result.Outcome)
            {
                case GesturePickerOutcome.Selected when result.GestureId is { } id:
                    UpdateCommand(command.Id, stored => stored with { Trigger = Trigger.ForGesture(id) });
                    break;
                case GesturePickerOutcome.NoGesture:
                    UpdateCommand(command.Id, stored => stored with { Trigger = Trigger.None });
                    break;
            }
        });
    }

    /// <summary>The Apps tab has no section to fall back to: without one selected, New command and Paste say so.</summary>
    private SectionId? RequireTarget(SectionId? target)
    {
        if (target is null)
        {
            Message = $"Select an app group first, or make one with {NewSectionLabel}";
        }

        return target;
    }

    private void UpdateCommand(CommandId id, Func<Command, Command> change)
    {
        var (group, command) = RequireCommand(id);
        _store.UpdateCommand(group.Id, change(command));
    }

    private (AppGroup Group, Command Command) RequireCommand(CommandId id)
        => _store.FindCommand(id) ?? throw new KeyNotFoundException("That command no longer exists.");
}
