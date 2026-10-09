using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// The rules for a group's hold remaps and the commands under them (F9, plan 0002), split out of <see cref="MappingRules"/>,
/// which runs them for every group it normalises and validates, so every store commit, load, import and sync result goes
/// through them. Hold remaps: names trimmed (an empty one is the hold key's name), unique within the group
/// case-insensitively and sorted by name; ids unique within the group; a hold key that is not a modifier (decision 2) and not
/// another hold remap's in the group (<see cref="KeyCode.None"/>, not chosen yet, any number of times); a tap time within
/// <see cref="MinTapTimeMs"/>..<see cref="MaxTapTimeMs"/>; used on at least one platform; never on the Global group
/// (decision 1). The command half is in <c>HoldRemapRules.Commands.cs</c>. A command whose <see cref="Command.HoldRemapId"/>
/// names no hold remap of its group is moved to the group's ordinary commands (<see cref="Detached"/>), never refused, as a
/// dangling category is cleared.
/// </summary>
public static partial class HoldRemapRules
{
    /// <summary>0 ms: the hold key is never sent (a key kept for its hold remap alone).</summary>
    public const int MinTapTimeMs = 0;

    /// <summary>Two seconds: a longer press is no tap by any reading.</summary>
    public const int MaxTapTimeMs = 2000;

    /// <summary>
    /// Trims and sorts the group's hold remaps (an empty name becomes the hold key's), detaches every command under a hold
    /// remap the group lacks, and clears the category of every command under one.
    /// </summary>
    public static AppGroup Normalised(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var holdRemaps = group.HoldRemaps
            .Select(holdRemap => holdRemap with { Name = NameOf(holdRemap) })
            .OrderBy(holdRemap => holdRemap.Name, MappingRules.NameComparer)
            .ToArray();
        var ids = holdRemaps.Select(holdRemap => holdRemap.Id).ToHashSet();
        var commands = group.Commands
            .Select(command => command.HoldRemapId is not { } id ? command
                : !ids.Contains(id) ? Detached(command)
                : command.CategoryId is null ? command
                : command with { CategoryId = null })
            .ToArray();
        return group with { HoldRemaps = holdRemaps, Commands = commands };
    }

    /// <summary>Checks a normalised group's hold remaps among themselves.</summary>
    public static void EnsureValid(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var accepted = new List<HoldRemap>();
        foreach (var holdRemap in group.HoldRemaps)
        {
            EnsureValid(holdRemap, group, accepted);
            accepted.Add(holdRemap);
        }
    }

    /// <summary>Checks a trimmed hold remap against the other hold remaps of its group (what an Add or an edit asks before it commits).</summary>
    public static void EnsureValid(HoldRemap holdRemap, AppGroup group, IEnumerable<HoldRemap> others)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(others);

        if (group.IsGlobal)
        {
            throw new MappingValidationException($"The Global group cannot have hold remaps: add '{holdRemap.Name}' to an app group.");
        }

        if (string.IsNullOrEmpty(holdRemap.Name))
        {
            throw new MappingValidationException($"A hold remap in '{group.Name}' needs a name.");
        }

        if (HotkeyKeys.IsModifier(holdRemap.HoldKey))
        {
            throw new MappingValidationException(
                $"{HotkeyText.KeyName(holdRemap.HoldKey)} cannot be a hold key: Ctrl, Alt, Shift and Win are already held for triggers.");
        }

        if (holdRemap.TapTimeMs is < MinTapTimeMs or > MaxTapTimeMs)
        {
            throw new MappingValidationException($"The tap time of '{holdRemap.Name}' must be between {MinTapTimeMs} and {MaxTapTimeMs} ms.");
        }

        if (holdRemap.UseOn == PlatformSet.None)
        {
            throw new MappingValidationException($"Use '{holdRemap.Name}' on at least one platform.");
        }

        foreach (var other in others)
        {
            if (other.Id == holdRemap.Id)
            {
                throw new MappingValidationException($"A hold remap with id {holdRemap.Id} already exists in '{group.Name}'.");
            }

            if (MappingRules.NameComparer.Equals(other.Name, holdRemap.Name))
            {
                throw new MappingValidationException($"A hold remap named '{other.Name}' already exists in '{group.Name}'.");
            }

            if (holdRemap.HoldKey != KeyCode.None && other.HoldKey == holdRemap.HoldKey)
            {
                throw new MappingValidationException($"'{other.Name}' in '{group.Name}' already uses {HotkeyText.KeyName(holdRemap.HoldKey)} as its hold key.");
            }
        }
    }

    /// <summary>The group's hold remap on <paramref name="holdKey"/>; null when it has none (or for <see cref="KeyCode.None"/>).</summary>
    public static HoldRemap? FindByHoldKey(AppGroup group, KeyCode holdKey)
    {
        ArgumentNullException.ThrowIfNull(group);
        return holdKey == KeyCode.None ? null : group.HoldRemaps.FirstOrDefault(holdRemap => holdRemap.HoldKey == holdKey);
    }

    /// <summary>
    /// <paramref name="command"/> as it lands in <paramref name="to"/> when it leaves <paramref name="from"/> (a move, a paste):
    /// under the hold remap of <paramref name="to"/> with the same hold key, else an ordinary command (<see cref="Detached"/>).
    /// </summary>
    public static Command Moved(Command command, AppGroup from, AppGroup to)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.HoldRemapOf(command) is not { } holdRemap)
        {
            return command.HoldRemapId is null ? command : Detached(command);
        }

        return FindByHoldKey(to, holdRemap.HoldKey) is { } target ? command with { HoldRemapId = target.Id } : Detached(command);
    }

    /// <summary>
    /// The command out of its hold remap, an ordinary command of its group: its input (the original's and an own version's)
    /// is cleared, since only a command under a hold remap has one; its steps stay.
    /// </summary>
    public static Command Detached(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var own = command.OwnVersion is { Trigger: Trigger.InputTrigger } version ? version with { Trigger = Trigger.None } : command.OwnVersion;
        return command with
        {
            HoldRemapId = null,
            Trigger = command.Trigger is Trigger.InputTrigger ? Trigger.None : command.Trigger,
            OwnVersion = own,
        };
    }

    private static string NameOf(HoldRemap holdRemap)
        => holdRemap.Name?.Trim() is { Length: > 0 } name ? name : HoldRemap.DefaultName(holdRemap.HoldKey);
}
