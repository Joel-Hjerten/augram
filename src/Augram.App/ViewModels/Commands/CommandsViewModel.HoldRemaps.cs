using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The hold remap half of <see cref="CommandsViewModel"/> (F9, plan 0002 step 4; the Apps tab): a hold remap is a section
/// nested in its app group's (<see cref="CommandSections"/>). New hold remap (the group's menu) adds one with no hold key yet,
/// selected so its form shows (<c>.HoldRemapPanel</c>); rename and the active box act on it; delete asks, then removes it with
/// its commands in one undo step (<see cref="MappingStore.RemoveHoldRemap"/>); copy takes it with its commands, and paste puts
/// a copy into the target's app group in one undo step (fresh ids, a free name for the hold remap, its commands' names as they
/// were; without its hold key when the group already uses that key, with the rule's words). The commands under it go through the ordinary command intents (<c>.Commands</c>).
/// </summary>
public sealed partial class CommandsViewModel
{
    private void NewHoldRemap(SectionItem section)
    {
        if (!section.CanAddHoldRemap)
        {
            Message = "A hold remap belongs to an app group: right-click a group on the Apps tab.";
            return;
        }

        var group = RequireGroup(section.Id.GroupId);
        var stem = HoldRemap.DefaultName(KeyCode.None);
        var names = group.HoldRemaps.Select(holdRemap => holdRemap.Name).ToList();
        var name = names.Contains(stem, MappingRules.NameComparer) ? FreeNames.Next(stem, names) : stem;
        var holdRemap = new HoldRemap(HoldRemapId.New(), name, KeyCode.None);
        var id = SectionId.ForHoldRemap(group.Id, holdRemap.Id);
        Expand(id);
        _store.AddHoldRemap(group.Id, holdRemap);
        Select(id, null);
        ProjectSelection();
    }

    private void RenameHoldRemap(GroupId groupId, HoldRemapId id, string name)
        => _store.UpdateHoldRemap(groupId, RequireHoldRemap(groupId, id) with { Name = name });

    private void ToggleHoldRemapActive(GroupId groupId, HoldRemapId id)
    {
        var holdRemap = RequireHoldRemap(groupId, id);
        _store.UpdateHoldRemap(groupId, holdRemap with { IsActive = !holdRemap.IsActive });
    }

    private async Task DeleteHoldRemapAsync(GroupId groupId, HoldRemapId id)
    {
        if (_store.FindGroup(groupId) is not { } group || group.FindHoldRemap(id) is not { } holdRemap)
        {
            Message = "That hold remap no longer exists.";
            return;
        }

        var count = group.Commands.Count(command => command.HoldRemapId == id);
        var question = count switch
        {
            0 => $"Delete hold remap '{holdRemap.Name}'? It has no commands.",
            1 => $"Delete hold remap '{holdRemap.Name}' and its command?",
            _ => $"Delete hold remap '{holdRemap.Name}' and its {count} commands?",
        };
        if (!await _confirm.ConfirmAsync("Delete hold remap", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var removed = _store.RemoveHoldRemap(groupId, id);
            Message = $"Deleted '{removed.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }

    private void CopyHoldRemap(GroupId groupId, HoldRemapId id)
    {
        var group = RequireGroup(groupId);
        var holdRemap = RequireHoldRemap(groupId, id);
        var commands = group.Commands.Where(command => command.HoldRemapId == id).ToList();
        _clipboard.HoldRemap = new HoldRemapCopy(holdRemap, commands);
        var what = commands.Count == 1 ? "its command" : $"its {commands.Count} commands";
        Message = $"Copied '{holdRemap.Name}' with {what}. Paste it into an app group with {CommandsKeymap.Current.Paste}.";
    }

    /// <summary>
    /// The copied hold remap into the target's app group with its commands, one undo step: fresh ids, a free name, the commands'
    /// names free among their new siblings (<see cref="CommandNames"/>: the new hold remap's commands, so they keep their names).
    /// When the rules refuse it (the group already uses the hold key) it is pasted without its hold key and the message says
    /// why; any other refusal shows as it is.
    /// </summary>
    private void PasteHoldRemap(HoldRemapCopy copy, SectionId target)
    {
        var group = RequireGroup(target.GroupId);
        var holdRemap = copy.HoldRemap with
        {
            Id = HoldRemapId.New(),
            Name = NameScope.Free(copy.HoldRemap.Name, group.HoldRemaps.Select(existing => existing.Name)),
        };
        var commands = new List<Command>(copy.Commands.Count);
        foreach (var command in copy.Commands)
        {
            var name = NameScope.Free(command.Name, CommandNames.SiblingNames([.. group.Commands, .. commands], holdRemap.Id));
            commands.Add(command with { Id = CommandId.New(), Name = name, HoldRemapId = holdRemap.Id, CategoryId = null });
        }

        var id = SectionId.ForHoldRemap(group.Id, holdRemap.Id);
        Expand(id);
        try
        {
            _store.UpdateGroup(group with { HoldRemaps = [.. group.HoldRemaps, holdRemap], Commands = [.. group.Commands, .. commands] });
        }
        catch (MappingValidationException refusal) when (holdRemap.HoldKey != KeyCode.None)
        {
            _store.UpdateGroup(group with { HoldRemaps = [.. group.HoldRemaps, holdRemap with { HoldKey = KeyCode.None }], Commands = [.. group.Commands, .. commands] });
            Message = $"Pasted '{holdRemap.Name}' into '{group.Name}' without its hold key: {refusal.Message}";
        }

        Select(id, null);
        ProjectSelection();
    }

    private HoldRemap RequireHoldRemap(GroupId groupId, HoldRemapId id)
        => RequireGroup(groupId).FindHoldRemap(id) ?? throw new KeyNotFoundException("That hold remap no longer exists.");
}
