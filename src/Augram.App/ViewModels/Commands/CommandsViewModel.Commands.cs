using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The command half of <see cref="CommandsViewModel"/> (F5a, F3): new, copy and paste, delete with confirmation, and the trigger (wheel, none, or the Select Gesture picker).</summary>
public sealed partial class CommandsViewModel
{
    /// <summary>"New command N" in the group, unbound and empty, selected and handed to the tree for renaming.</summary>
    private void NewCommand(GroupId groupId)
    {
        var group = _store.FindGroup(groupId) ?? _store.Global;
        _expanded.Add(group.Id);
        var stored = _store.AddCommand(group.Id, new Command(CommandId.New(), NextCommandName(group), Trigger.None, IsActive: true, Steps: []));
        Select(group.Id, stored.Id);
        ProjectSelection();
        RenameRequested?.Invoke(this, stored.Id);
    }

    private void CopyCommand(CommandItem command)
    {
        _clipboard.Command = RequireCommand(command.Id).Command;
        Message = $"Copied '{command.Name}'. Paste it into a group with {CommandsKeymap.Current.Paste}.";
    }

    /// <summary>A copy with a fresh id and a free name; when the group already uses the trigger (A7) it is pasted unbound rather than refused.</summary>
    private void PasteCommand(GroupId groupId)
    {
        if (_clipboard.Command is not { } source)
        {
            Message = "Nothing to paste: copy a command first.";
            return;
        }

        var group = _store.FindGroup(groupId) ?? _store.Global;
        _expanded.Add(group.Id);
        var copy = source with { Id = CommandId.New(), Name = UniqueName(group, source.Name) };
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

        Select(group.Id, stored.Id);
        ProjectSelection();
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

    /// <summary>"New command N" with the smallest N the group does not have yet.</summary>
    private static string NextCommandName(AppGroup group)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"New command {n}";
            if (!group.Commands.Any(command => MappingRules.NameComparer.Equals(command.Name, candidate)))
            {
                return candidate;
            }
        }
    }

    /// <summary>The name itself when the group lacks it, else "name copy", "name copy 2", …</summary>
    private static string UniqueName(AppGroup group, string name)
    {
        bool Taken(string candidate) => group.Commands.Any(command => MappingRules.NameComparer.Equals(command.Name, candidate));
        if (!Taken(name))
        {
            return name;
        }

        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? $"{name} copy" : $"{name} copy {n}";
            if (!Taken(candidate))
            {
                return candidate;
            }
        }
    }

    private void UpdateCommand(CommandId id, Func<Command, Command> change)
    {
        var (group, command) = RequireCommand(id);
        _store.UpdateCommand(group.Id, change(command));
    }

    private (AppGroup Group, Command Command) RequireCommand(CommandId id)
        => _store.FindCommand(id) ?? throw new KeyNotFoundException("That command no longer exists.");
}
