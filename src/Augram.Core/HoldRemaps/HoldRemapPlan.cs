using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// What the hook needs to answer "is this a hold key, and is that an input of it?" for one app group on one platform (F9,
/// plan 0002): one <see cref="HoldRemapEntry"/> per active hold remap used here with a hold key chosen, each with the active
/// commands under it that are used here (<see cref="AppGroup.IsCommandUsedOn"/>: group, hold remap and command) and have an
/// input on this platform. Built off the hook thread (the engine's watch, when the foreground app or the mapping changes)
/// and immutable, so the hook reads it through one volatile reference and <see cref="Find"/> allocates nothing.
/// </summary>
public sealed class HoldRemapPlan
{
    private readonly HoldRemapEntry[] _entries;

    private HoldRemapPlan(GroupId? groupId, string? groupName, HoldRemapEntry[] entries)
    {
        GroupId = groupId;
        GroupName = groupName;
        _entries = entries;
    }

    /// <summary>No hold remaps: every key and button is left to the rest of the engine.</summary>
    public static HoldRemapPlan Empty { get; } = new(null, null, []);

    /// <summary>The app group the plan was made for; null for <see cref="Empty"/>.</summary>
    public GroupId? GroupId { get; }

    /// <summary>The group's name, for the log.</summary>
    public string? GroupName { get; }

    public IReadOnlyList<HoldRemapEntry> Entries => _entries;

    public bool IsEmpty => _entries.Length == 0;

    /// <summary>The hold remap on <paramref name="holdKey"/>, or null: the hook's question at a key press.</summary>
    public HoldRemapEntry? Find(KeyCode holdKey)
    {
        foreach (var entry in _entries)
        {
            if (entry.HoldKey == holdKey)
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// True when some app group could give the foreground a non-empty plan on <paramref name="platform"/>: an active group used
    /// here, whose matcher can match a window here (<see cref="IgnoreList.CanMatchOn"/>), with an active hold remap used here
    /// and a hold key chosen. The engine watches focus for hold remaps only then.
    /// </summary>
    public static bool WatchesFocus(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Groups.Any(group => !group.IsGlobal
            && group.IsActive
            && group.IsUsedOn(platform)
            && group.Matcher is { } matcher
            && IgnoreList.CanMatchOn(matcher, platform)
            && group.HoldRemaps.Any(holdRemap => holdRemap.IsActive && holdRemap.IsUsedOn(platform) && holdRemap.HoldKey != KeyCode.None));
    }

    /// <summary>
    /// The plan for the app group whose window is <paramref name="foreground"/> (F9: the hold key belongs to the app in front
    /// when it goes down, plan 0002 decision 8): the first active app group that claims it, as the resolver finds it.
    /// </summary>
    public static HoldRemapPlan For(MappingDocument mapping, WindowIdentity? foreground, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return ForGroup(CommandResolver.FindGroup(mapping, foreground, platform), platform);
    }

    /// <summary>The plan for <paramref name="group"/> on <paramref name="platform"/>; <see cref="Empty"/> for none, the Global group, or a group inactive or not used here.</summary>
    public static HoldRemapPlan ForGroup(AppGroup? group, HostPlatform platform)
    {
        if (group is null || group.IsGlobal || !group.IsActive || !group.IsUsedOn(platform))
        {
            return Empty;
        }

        var entries = new List<HoldRemapEntry>();
        foreach (var holdRemap in group.HoldRemaps)
        {
            if (holdRemap.IsActive && holdRemap.IsUsedOn(platform) && holdRemap.HoldKey != KeyCode.None)
            {
                entries.Add(new HoldRemapEntry(holdRemap, Bindings(group, holdRemap, platform)));
            }
        }

        return entries.Count == 0 ? Empty : new HoldRemapPlan(group.Id, group.Name, [.. entries]);
    }

    private static IEnumerable<HoldBinding> Bindings(AppGroup group, HoldRemap holdRemap, HostPlatform platform)
    {
        foreach (var command in group.Commands)
        {
            if (command.HoldRemapId == holdRemap.Id
                && command.IsActive
                && group.IsCommandUsedOn(command, platform)
                && command.TriggerFor(platform) is Trigger.InputTrigger { Input: var input })
            {
                yield return Binding(command, input, platform);
            }
        }
    }

    /// <summary>A Remap command (its one active step a Remap whose output is set), a Steps command (any active step), or one that does nothing.</summary>
    private static HoldBinding Binding(Command command, HoldInput input, HostPlatform platform)
    {
        var active = command.PlanFor(platform).Where(step => step.Stored.IsActive).ToArray();
        var binding = new HoldBinding(command.Id, command.Name, input);
        if (active.Any(step => step.Stored.Step is RemapStep))
        {
            return active is [{ Run.Step: RemapStep { Output.IsSet: true } remap }] ? binding with { Output = remap.Output } : binding;
        }

        return binding with { RunsSteps = active.Length > 0 };
    }
}
