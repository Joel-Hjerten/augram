using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// The command part of <see cref="SyncDocumentBuilder"/>: puts every merged command into its group (Global
/// when the group is gone), clears references the merge left dangling, then makes names unique and binds
/// each trigger once per group (A7), the commands already here first. Every change is a <see cref="SyncRepair"/>.
/// </summary>
internal sealed class CommandPlacement
{
    private readonly Dictionary<GroupId, string> _groupNames;
    private readonly Dictionary<GroupId, List<CommandCategory>> _categories;
    private readonly HashSet<GestureId> _gestures;
    private readonly SyncItemSet _before;
    private readonly List<SyncRepair> _repairs;

    public CommandPlacement(
        IReadOnlyList<AppGroup> groups,
        Dictionary<GroupId, List<CommandCategory>> categories,
        HashSet<GestureId> gestures,
        SyncItemSet before,
        List<SyncRepair> repairs)
    {
        _groupNames = groups.ToDictionary(group => group.Id, group => group.Name);
        _categories = categories;
        _gestures = gestures;
        _before = before;
        _repairs = repairs;
    }

    /// <summary>The commands of every group (every group has an entry, maybe empty).</summary>
    public Dictionary<GroupId, List<Command>> Place(IEnumerable<SyncItem.CommandItem> items, IReadOnlySet<SyncItemKey> incoming)
    {
        var placed = _groupNames.Keys.ToDictionary(id => id, _ => new List<(Command Command, SyncItemKey Key, bool Incoming)>());
        foreach (var item in items)
        {
            var (groupId, command, isIncoming) = Located(item, incoming.Contains(item.Key));
            placed[groupId].Add((Unreferenced(command, groupId, item.Key, isIncoming), item.Key, isIncoming));
        }

        return placed.ToDictionary(pair => pair.Key, pair => Unique(pair.Key, pair.Value));
    }

    /// <summary>The command's group, or Global (uncategorized) when its group is gone; a displaced command counts as incoming.</summary>
    private (GroupId Group, Command Command, bool Incoming) Located(SyncItem.CommandItem item, bool isIncoming)
    {
        if (_groupNames.ContainsKey(item.GroupId))
        {
            return (item.GroupId, item.Command, isIncoming);
        }

        _repairs.Add(new(item.Key, SyncRepairKind.MovedToGlobal, $"Command '{item.Command.Name}' moved to Global: its app group is gone."));
        return (GroupId.Global, item.Command with { CategoryId = null }, true);
    }

    /// <summary>
    /// Clears a gesture trigger whose gesture is gone (only when the merge removed it or the command is incoming:
    /// a reference that was already dangling here is not the merge's to touch) and a category that is gone.
    /// </summary>
    private Command Unreferenced(Command command, GroupId groupId, SyncItemKey key, bool isIncoming)
    {
        if (command.Trigger is Trigger.GestureTrigger trigger
            && !_gestures.Contains(trigger.GestureId)
            && (isIncoming || _before.Contains(SyncItemKey.ForGesture(trigger.GestureId))))
        {
            _repairs.Add(new(key, SyncRepairKind.TriggerCleared, $"Command '{command.Name}' in '{_groupNames[groupId]}' unbound: its gesture is gone."));
            command = command with { Trigger = Trigger.None };
        }

        if (command.CategoryId is { } category && !_categories[groupId].Any(candidate => candidate.Id == category))
        {
            _repairs.Add(new(key, SyncRepairKind.CategoryCleared, $"Command '{command.Name}' in '{_groupNames[groupId]}' is now uncategorized: its category is gone."));
            command = command with { CategoryId = null };
        }

        return command;
    }

    private List<Command> Unique(GroupId groupId, List<(Command Command, SyncItemKey Key, bool Incoming)> entries)
    {
        var names = new SyncNames();
        var bound = new Dictionary<Trigger, string>();
        var result = new List<Command>(entries.Count);
        foreach (var (original, key, _) in entries.OrderBy(entry => entry.Incoming ? 1 : 0))
        {
            var command = original;
            var name = names.Claim(command.Name);
            if (name != command.Name)
            {
                _repairs.Add(new(key, SyncRepairKind.Renamed, $"Incoming command '{command.Name}' in '{_groupNames[groupId]}' renamed '{name}': the name is taken."));
                command = command with { Name = name };
            }

            if (command.Trigger.IsBound && !bound.TryAdd(command.Trigger, command.Name))
            {
                _repairs.Add(new(key, SyncRepairKind.Unbound, $"Incoming command '{command.Name}' in '{_groupNames[groupId]}' unbound: '{bound[command.Trigger]}' already uses {command.Trigger.Describe()}."));
                command = command with { Trigger = Trigger.None };
            }

            result.Add(command);
        }

        return result;
    }
}
