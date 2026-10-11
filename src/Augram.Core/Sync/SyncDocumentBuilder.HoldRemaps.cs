using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Sync;

/// <summary>
/// The hold remap part of <see cref="SyncDocumentBuilder"/> (F9, sync format 11). Hold remaps are placed like categories: one
/// whose group is gone (or is Global, which has none) is dropped; an incoming one is renamed " (2)" on a name clash and loses
/// its hold key when another hold remap of the group holds it already. A command under a hold remap the merge removed is made
/// ordinary by <see cref="CommandPlacement"/>. Last, every command under a hold remap is checked against the hold remap rules as
/// they span items (an input that is now the hold key, an own version's Remap output that no longer suits the input): one that
/// breaks them is unbound on every platform rather than failing the merge.
/// </summary>
internal static partial class SyncDocumentBuilder
{
    private static Dictionary<GroupId, List<HoldRemap>> HoldRemaps(
        SyncItem.HoldRemapItem[] items,
        IReadOnlySet<SyncItemKey> incoming,
        List<AppGroup> groups,
        List<SyncRepair> repairs)
    {
        var byGroup = groups.ToDictionary(group => group.Id, _ => new List<HoldRemap>());
        var groupNames = groups.ToDictionary(group => group.Id, group => group.Name);
        var names = groups.ToDictionary(group => group.Id, _ => new NameScope());
        foreach (int index in IncomingLast(items, incoming))
        {
            var item = items[index];
            var holdRemap = item.HoldRemap;
            if (item.GroupId == GroupId.Global || !byGroup.TryGetValue(item.GroupId, out var list))
            {
                var why = item.GroupId == GroupId.Global ? "the Global group has none" : "its app group is gone";
                repairs.Add(new(item.Key, SyncRepairKind.HoldRemapDropped, $"Hold remap '{holdRemap.Name}' dropped: {why}."));
                continue;
            }

            var name = names[item.GroupId].Claim(holdRemap.Name);
            if (name != holdRemap.Name)
            {
                repairs.Add(new(item.Key, SyncRepairKind.Renamed, $"Incoming hold remap '{holdRemap.Name}' in '{groupNames[item.GroupId]}' renamed '{name}': the name is taken."));
                holdRemap = holdRemap with { Name = name };
            }

            if (holdRemap.HoldKey != KeyCode.None && list.FirstOrDefault(other => other.HoldKey == holdRemap.HoldKey) is { } holder)
            {
                repairs.Add(new(item.Key, SyncRepairKind.HoldKeyCleared, $"Incoming hold remap '{holdRemap.Name}' in '{groupNames[item.GroupId]}' has no hold key now: '{holder.Name}' already uses {HotkeyText.KeyName(holdRemap.HoldKey)}."));
                holdRemap = holdRemap with { HoldKey = KeyCode.None };
            }

            list.Add(holdRemap);
        }

        return byGroup;
    }

    /// <summary>
    /// The group with every command under a hold remap that breaks the hold remap rules unbound on every platform, and every
    /// ordinary command whose own version brought an input back (its own-steps item arrives after the command was made
    /// ordinary) without that input; each with a repair line.
    /// </summary>
    private static AppGroup WithHoldRemapCommandsChecked(AppGroup group, List<SyncRepair> repairs)
    {
        var commands = group.Commands.ToArray();
        for (var i = 0; i < commands.Length; i++)
        {
            var command = commands[i];
            var key = SyncItemKey.ForCommand(command.Id);
            if (command.HoldRemapId is null)
            {
                if (command.OwnVersion?.Trigger is Trigger.InputTrigger)
                {
                    repairs.Add(new(key, SyncRepairKind.HoldRemapCleared, $"The own input of '{command.Name}' in '{group.Name}' unbound: the command is not under a hold remap."));
                    commands[i] = HoldRemapRules.Detached(command);
                }
            }
            else if (Problem(command, group) is { } problem)
            {
                repairs.Add(new(key, SyncRepairKind.Unbound, $"Command '{command.Name}' {CommandNames.Where(group, command)} unbound: {problem}"));
                var own = command.OwnVersion is { Trigger: not null } version ? version with { Trigger = Trigger.None } : command.OwnVersion;
                commands[i] = command with { Trigger = Trigger.None, OwnVersion = own };
            }
        }

        return group with { Commands = commands };
    }

    private static string? Problem(Command command, AppGroup group)
    {
        try
        {
            HoldRemapRules.EnsureValid(command, group);
            return null;
        }
        catch (MappingValidationException ex)
        {
            return ex.Message;
        }
    }
}
