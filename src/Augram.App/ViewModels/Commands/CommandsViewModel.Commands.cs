using Augram.App.Components.CommandTree;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The command half of <see cref="CommandsViewModel"/> (F5a, F3): new and paste into the selected section
/// (its group, on the Global tab its category, on the Apps tab a hold remap of the group, plan 0002), copy, delete with
/// confirmation and the category; the trigger is <c>CommandsViewModel.Trigger.cs</c>, an input <c>.Input</c>.
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>Where New command and Paste go: the section acted on, else the selected one; Uncategorized on the Global tab when there is neither.</summary>
    private SectionId? TargetOf(SectionItem? section)
        => section?.Id ?? SelectedSectionId ?? (Scope == CommandsScope.Global ? SectionId.Uncategorized : null);

    /// <summary>"New command N" in the section (N free among the commands of its parent: <see cref="CommandNames"/>), unbound and empty, selected and handed to the tree for renaming.</summary>
    private void NewCommand(SectionId? target)
    {
        if (RequireTarget(target) is not { } section)
        {
            return;
        }

        var group = RequireGroup(section.GroupId);
        Expand(section);
        var name = FreeNames.Next("New command", CommandNames.SiblingNames(group.Commands, section.HoldRemapId));
        var command = new Command(CommandId.New(), name, Trigger.None, IsActive: true, Steps: [], CategoryId: section.CategoryId) { HoldRemapId = section.HoldRemapId };
        var stored = _store.AddCommand(group.Id, command);
        Select(section, stored.Id);
        ProjectSelection();
        RenameRequested?.Invoke(this, stored.Id);
    }

    private void CopyCommand(CommandItem command)
    {
        _clipboard.Command = RequireCommand(command.Id).Command;
        Message = $"Copied '{command.Name}'. Paste it into a group or a category with {CommandsKeymap.Current.Paste}.";
    }

    /// <summary>
    /// A copy with a fresh id and a name free among the commands of the parent it lands in (<see cref="CommandNames"/>: a hold
    /// remap's, or the group's ordinary commands); when the group already uses the trigger (A7) it is pasted unbound rather than
    /// refused. Into a hold remap (plan 0002) it lands under it, keeping an input but no other trigger; elsewhere it lands an
    /// ordinary command, without the input it had under a hold remap. A copied hold remap pastes into the target's group.
    /// </summary>
    private void PasteCommand(SectionId? target)
    {
        if (_clipboard.HoldRemap is { } copiedHoldRemap)
        {
            if (RequireTarget(target) is { } groupTarget)
            {
                PasteHoldRemap(copiedHoldRemap, groupTarget);
            }

            return;
        }

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
        Expand(section);
        var name = FreeNames.CopyOf(source.Name, CommandNames.SiblingNames(group.Commands, section.HoldRemapId));
        var copy = Placed(source with { Id = CommandId.New(), Name = name, CategoryId = PastedCategory(section, group, source), HoldRemapId = section.HoldRemapId }, section);
        Command stored;
        try
        {
            stored = _store.AddCommand(group.Id, copy);
        }
        catch (MappingValidationException refusal) when (copy.Trigger.IsBound || copy.OwnVersion?.Trigger is not null)
        {
            // Unbound on every platform: the original's trigger and an own version's.
            stored = _store.AddCommand(group.Id, copy with { Trigger = Trigger.None, OwnVersion = copy.OwnVersion is { } own ? own with { Trigger = null } : null });
            Message = group.HoldRemapOf(copy) is { } holdRemap
                ? $"Pasted '{stored.Name}' under '{holdRemap.Name}' without its trigger: {refusal.Message}"
                : $"Pasted '{stored.Name}' into '{group.Name}' without its trigger: that trigger is already used there.";
        }

        Select(section, stored.Id);
        ProjectSelection();
    }

    /// <summary>
    /// The pasted copy outside a hold remap: an ordinary command, without the input it had under one
    /// (<see cref="HoldRemapRules.Detached"/>, Core's own move rule); the message says so. Under a hold remap it is pasted as it
    /// is, and a trigger the rules refuse there (a gesture, an input already used) is dropped with their words.
    /// </summary>
    private Command Placed(Command copy, SectionId target)
    {
        if (target.HoldRemapId is not null)
        {
            return copy;
        }

        var detached = HoldRemapRules.Detached(copy);
        if (detached != copy)
        {
            Message = $"Pasted '{copy.Name}' without its input: only a command under a hold remap has one.";
        }

        return detached;
    }

    /// <summary>
    /// A Global section decides the category (Uncategorized included); an app group keeps the copied
    /// command's category when it has one of the same name, else the copy is uncategorized there; under a hold remap it has none.
    /// </summary>
    private CategoryId? PastedCategory(SectionId target, AppGroup group, Command source)
    {
        if (group.IsGlobal)
        {
            return target.CategoryId;
        }

        if (target.HoldRemapId is not null)
        {
            return null;
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
        if (Expand(CommandSections.SectionOf(Scope, RequireGroup(group.Id), updated)))
        {
            Project();
        }
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
