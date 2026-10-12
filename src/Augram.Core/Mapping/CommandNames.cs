using Augram.Core.HoldRemaps;

namespace Augram.Core.Mapping;

/// <summary>
/// How commands are named (Joel, 2026-10-10). A command's name is unique within its <b>parent</b>, case-insensitively
/// (<see cref="MappingRules.NameComparer"/>): the hold remap it sits under (F9), or its group's ordinary commands. So two hold
/// remaps of one group may each have an "Orbit", and so may a hold remap and the group's ordinary commands; ids, never names,
/// identify a command. <see cref="MappingRules"/> refuses a clash between <see cref="AreSiblings">siblings</see>; everything
/// that picks a free name (New command, Paste, a pasted hold remap's commands, the sync's rename on a clash, the importer's
/// merge) asks <see cref="SiblingNames"/> for the names taken, so the scope is decided here only. <see cref="Label(AppGroup, Command)"/>
/// is the command as a log line or a list names it, and <see cref="Where(AppGroup, Command)"/> where it is, as a sentence says it.
/// </summary>
public static class CommandNames
{
    /// <summary>Between the parts of a <see cref="Label(AppGroup, Command)"/>.</summary>
    public const string Separator = " › ";

    /// <summary>True when the two commands share a parent (the same hold remap, or both ordinary commands): their names must differ.</summary>
    public static bool AreSiblings(Command command, Command other)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(other);
        return command.HoldRemapId == other.HoldRemapId;
    }

    /// <summary>
    /// The names a command under <paramref name="parent"/> (null: an ordinary command) cannot take among
    /// <paramref name="commands"/> (one group's): those of the commands with the same parent.
    /// </summary>
    public static IEnumerable<string> SiblingNames(IEnumerable<Command> commands, HoldRemapId? parent)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return commands.Where(command => command.HoldRemapId == parent).Select(command => command.Name);
    }

    /// <summary>
    /// Where a command sits, the one label every list, message and log line uses (plan 0007 Q9): "Blender › Space › Orbit" for a
    /// command under a hold remap, "Global › Media › Volume up" for a Global command in its category, "Blender › Orbit" for an
    /// ordinary app command, and "Global › Minimize" for a Global command in Uncategorized (Joel, 2026-10-12: the segment says
    /// nothing) or whose category is not in the group, as outside the store.
    /// </summary>
    public static string Label(AppGroup group, Command command)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        return Label(group.Name, Parent(group, command), command.Name);
    }

    /// <summary><see cref="Label(AppGroup, Command)"/> from the names alone (<paramref name="parent"/>, the hold remap's or the Global category's name; null for none).</summary>
    public static string Label(string group, string? parent, string command)
        => parent is null ? group + Separator + command : group + Separator + parent + Separator + command;

    /// <summary>
    /// The middle part of a <see cref="Label(AppGroup, Command)"/>: the hold remap the command is under, else a Global command's
    /// category other than Uncategorized (<see cref="CategoryRules.Uncategorized"/>, by id); null for none.
    /// </summary>
    public static string? Parent(AppGroup group, Command command)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        if (group.HoldRemapOf(command) is { } holdRemap)
        {
            return holdRemap.Name;
        }

        return group.IsGlobal && group.CategoryOf(command) is { } category && category.Id != CategoryRules.Uncategorized(group)?.Id ? category.Name : null;
    }

    /// <summary>"under 'Space' in 'Blender'" for a command under a hold remap, "in 'Blender'" for an ordinary one: the rules' and the sync's sentences.</summary>
    public static string Where(AppGroup group, Command command)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        return Where(group.Name, group.HoldRemapOf(command)?.Name);
    }

    /// <summary><see cref="Where(AppGroup, Command)"/> from the names alone (<paramref name="holdRemap"/> null: an ordinary command).</summary>
    public static string Where(string group, string? holdRemap)
        => holdRemap is null ? $"in '{group}'" : $"under '{holdRemap}' in '{group}'";
}
