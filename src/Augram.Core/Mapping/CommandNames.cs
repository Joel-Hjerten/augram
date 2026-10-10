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

    /// <summary>"Blender › Space › Orbit" for a command under a hold remap, "Blender › Orbit" for an ordinary one.</summary>
    public static string Label(AppGroup group, Command command)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        return Label(group.Name, group.HoldRemapOf(command)?.Name, command.Name);
    }

    /// <summary><see cref="Label(AppGroup, Command)"/> from the names alone (<paramref name="holdRemap"/> null: an ordinary command).</summary>
    public static string Label(string group, string? holdRemap, string command)
        => holdRemap is null ? group + Separator + command : group + Separator + holdRemap + Separator + command;

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
