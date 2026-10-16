using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// One hold-key conflict (plan 0012 decision 10): <see cref="Key"/> is on the While holding list of <see cref="Entry"/> and is
/// the hold key of <see cref="HoldRemap"/> in <see cref="Group"/>, an app group matching the same app. Augram swallows a hold
/// key, so while that hold remap runs the key never ignores. Both forms word it from their side.
/// </summary>
public sealed record HoldKeyConflict(IgnoredApp Entry, KeyCode Key, AppGroup Group, HoldRemap HoldRemap)
{
    /// <summary>"Blender › Navigate": the hold remap labelled as its commands' parent is (<see cref="CommandNames.Label(string, string?, string)"/>).</summary>
    public string HoldRemapLabel => CommandNames.Label(Group.Name, null, HoldRemap.Name);

    /// <summary>The entry's row: "Space is the hold key of Blender › Navigate; while Navigate runs it never ignores."</summary>
    public string OnEntry(HostPlatform names)
        => $"{HotkeyText.KeyName(Key, names)} is the hold key of {HoldRemapLabel}; while {HoldRemap.Name} runs it never ignores.";

    /// <summary>The hold remap's hold key field: "Space is on Exclusions › While holding for Blender; while Navigate runs it never ignores there."</summary>
    public string OnHoldRemap(HostPlatform names)
        => $"{HotkeyText.KeyName(Key, names)} is on Exclusions › While holding for {Entry.Name}; while {HoldRemap.Name} runs it never ignores there.";
}

/// <summary>
/// The hold-key conflict query (plan 0012 decision 10), the one home both forms ask (Exclusions › While holding's rows and a
/// hold remap's hold key field): on one platform, the inputs of the While holding entries that are the hold key of a hold
/// remap in an app group matching the same app, decided from the matchers alone (<see cref="AppMatcher.SharesExecutableWith"/>:
/// an executable name in common there, case-insensitive, regex lists ignored). Only what takes part there counts: active
/// entries, groups and hold remaps used on that platform, with a hold key chosen. Pure.
/// </summary>
public static class HoldKeyConflicts
{
    /// <summary>Every conflict on <paramref name="platform"/>, entries in list order, then groups in document order.</summary>
    public static IReadOnlyList<HoldKeyConflict> On(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        List<HoldKeyConflict>? conflicts = null;
        foreach (var entry in mapping.Ignored)
        {
            if (!entry.IsActive || !entry.IsWhileHolding)
            {
                continue;
            }

            var keys = entry.HeldInputsOn(platform).OfType<KeyOrButton.Key>().Select(key => key.KeyCode).ToArray();
            if (keys.Length == 0)
            {
                continue;
            }

            foreach (var group in mapping.Groups)
            {
                if (!group.IsActive || !group.IsUsedOn(platform) || group.Matcher is not { } matcher || !matcher.SharesExecutableWith(entry.Matcher, platform))
                {
                    continue;
                }

                foreach (var holdRemap in group.HoldRemaps)
                {
                    if (holdRemap.IsActive && holdRemap.IsUsedOn(platform) && holdRemap.HoldKey != KeyCode.None && keys.Contains(holdRemap.HoldKey))
                    {
                        (conflicts ??= []).Add(new HoldKeyConflict(entry, holdRemap.HoldKey, group, holdRemap));
                    }
                }
            }
        }

        return conflicts ?? (IReadOnlyList<HoldKeyConflict>)[];
    }

    /// <summary>The conflicts of one While holding entry on <paramref name="platform"/> (its rows' warnings).</summary>
    public static IReadOnlyList<HoldKeyConflict> ForEntry(MappingDocument mapping, GroupId entry, HostPlatform platform)
        => [.. On(mapping, platform).Where(conflict => conflict.Entry.Id == entry)];

    /// <summary>The conflicts of one hold remap on <paramref name="platform"/> (its hold key field's warning).</summary>
    public static IReadOnlyList<HoldKeyConflict> ForHoldRemap(MappingDocument mapping, GroupId group, HoldRemapId holdRemap, HostPlatform platform)
        => [.. On(mapping, platform).Where(conflict => conflict.Group.Id == group && conflict.HoldRemap.Id == holdRemap)];
}
