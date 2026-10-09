using Augram.Core.HoldRemaps;

namespace Augram.Core.Mapping;

/// <summary>
/// The hold remap mutators of <see cref="MappingStore"/> (F9, plan 0002): add, update and remove a group's hold remap, one
/// undo step each, validated like every other change (<see cref="HoldRemapRules"/>). The commands under a hold remap go in and
/// out through the ordinary command mutators (a command with <see cref="Command.HoldRemapId"/> set is under it).
/// </summary>
public sealed partial class MappingStore
{
    /// <summary>Adds the hold remap to the group; returns it as stored (trimmed, named after its hold key when unnamed).</summary>
    public HoldRemap AddHoldRemap(GroupId groupId, HoldRemap holdRemap)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        var group = RequireGroup(groupId);
        return UpdateGroup(group with { HoldRemaps = [.. group.HoldRemaps, holdRemap] }).FindHoldRemap(holdRemap.Id)!;
    }

    /// <summary>Replaces the group's hold remap with the same id (its form's edits: name, hold key, tap time, active, Use on).</summary>
    /// <exception cref="KeyNotFoundException">The group has no hold remap with that id.</exception>
    public HoldRemap UpdateHoldRemap(GroupId groupId, HoldRemap holdRemap)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        var group = RequireGroup(groupId);
        var holdRemaps = group.HoldRemaps.ToArray();
        holdRemaps[IndexOfHoldRemap(group, holdRemap.Id)] = holdRemap;
        return UpdateGroup(group with { HoldRemaps = holdRemaps }).FindHoldRemap(holdRemap.Id)!;
    }

    /// <summary>
    /// Removes the hold remap and every command under it, one undo step (the UI asks first, as for deleting a group);
    /// <see cref="Undo"/> brings them back.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The group has no hold remap with that id.</exception>
    public HoldRemap RemoveHoldRemap(GroupId groupId, HoldRemapId id)
    {
        var group = RequireGroup(groupId);
        var removed = group.HoldRemaps[IndexOfHoldRemap(group, id)];
        UpdateGroup(group with
        {
            HoldRemaps = group.HoldRemaps.Where(holdRemap => holdRemap.Id != id).ToArray(),
            Commands = group.Commands.Where(command => command.HoldRemapId != id).ToArray(),
        });
        return removed;
    }

    private static int IndexOfHoldRemap(AppGroup group, HoldRemapId id)
    {
        int index = IndexOf(group.HoldRemaps, holdRemap => holdRemap.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"No hold remap with id {id} in '{group.Name}'.");
    }
}
