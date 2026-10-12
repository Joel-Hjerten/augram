using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// Turns merged items back into a gesture list and a mapping that pass <see cref="GestureRules"/> and
/// <see cref="MappingRules"/>, repairing what they would refuse and reporting each repair (README: repairs).
/// Items that were already here are placed first and keep their names and triggers; incoming items (taken
/// from the other side, or displaced by the merge) are the ones renamed " (2)" or unbound. A command whose
/// group is gone moves into Global; a gesture trigger is cleared when the merge removed the gesture (or the
/// command is incoming); a category reference is cleared when the category is gone; a category whose group
/// is gone is dropped; a command's own version (F8) goes back onto its command, and is dropped when the command is gone.
/// Hold remaps (F9) are repaired like categories, in <c>SyncDocumentBuilder.HoldRemaps.cs</c>.
/// Gestures keep the order of <c>merged</c>.
/// </summary>
internal static partial class SyncDocumentBuilder
{
    public static Built Build(IReadOnlyList<SyncItem> merged, IReadOnlySet<SyncItemKey> incoming, SyncItemSet before)
    {
        var repairs = new List<SyncRepair>();
        var gestures = Gestures(merged.OfType<SyncItem.GestureItem>().ToArray(), incoming, repairs);
        var groups = Groups(merged.OfType<SyncItem.GroupItem>().ToArray(), incoming, repairs);
        var categories = Categories(merged.OfType<SyncItem.CategoryItem>().ToArray(), incoming, groups, repairs);
        var holdRemaps = HoldRemaps(merged.OfType<SyncItem.HoldRemapItem>().ToArray(), incoming, groups, repairs);
        var commands = new CommandPlacement(groups, categories, holdRemaps, gestures.Select(gesture => gesture.Id).ToHashSet(), before, repairs)
            .Place(merged.OfType<SyncItem.CommandItem>(), incoming);
        var ignored = Ignored(merged.OfType<SyncItem.IgnoredItem>().ToArray(), incoming, repairs);
        var versions = Versions(merged.OfType<SyncItem.VersionItem>().ToArray(), commands, repairs);
        var gestureIds = gestures.Select(gesture => gesture.Id).ToHashSet();

        var document = new MappingDocument(
            groups.Select(group => WithHoldRemapCommandsChecked(
                group with
                {
                    Categories = categories[group.Id],
                    HoldRemaps = holdRemaps[group.Id],
                    Commands = OwnTriggersChecked(
                        [.. commands[group.Id].Select(command => versions.TryGetValue(command.Id, out var own) ? command with { OwnVersion = own } : command)],
                        group with { HoldRemaps = holdRemaps[group.Id] },
                        gestureIds,
                        repairs),
                },
                repairs)).ToArray(),
            ignored);
        var validGestures = GestureRules.ValidSet(gestures);
        var validMapping = MappingRules.ValidDocument(document);
        return new Built(validGestures, validMapping, SyncItemSet.From(validGestures, validMapping), repairs);
    }

    /// <summary>Indexes of <paramref name="items"/> with the ones already here first and the incoming ones last, each in their original order.</summary>
    internal static IEnumerable<int> IncomingLast(IReadOnlyList<SyncItem> items, IReadOnlySet<SyncItemKey> incoming)
        => Enumerable.Range(0, items.Count).OrderBy(index => incoming.Contains(items[index].Key) ? 1 : 0);

    /// <summary>
    /// The name an item takes in <paramref name="names"/>: its own, or the first free " (2)", " (3)"… when it is taken, with a
    /// <see cref="SyncRepairKind.Renamed"/> line ("Incoming <paramref name="noun"/> 'Orbit' <paramref name="where"/> renamed
    /// 'Orbit (2)': the name is taken."). Every rename the rebuild makes goes through here; the caller has the items already
    /// here claim first (<see cref="IncomingLast"/>), so the incoming one is the one renamed.
    /// </summary>
    internal static string Claimed(NameScope names, string name, SyncItemKey key, string noun, string? where, List<SyncRepair> repairs)
    {
        var claimed = names.Claim(name);
        if (claimed != name)
        {
            var place = where is null ? string.Empty : " " + where;
            repairs.Add(new(key, SyncRepairKind.Renamed, $"Incoming {noun} '{name}'{place} renamed '{claimed}': the name is taken."));
        }

        return claimed;
    }

    private static Gesture[] Gestures(SyncItem.GestureItem[] items, IReadOnlySet<SyncItemKey> incoming, List<SyncRepair> repairs)
        => NamesUnique(items, [.. items.Select(item => item.Gesture)], incoming, gesture => gesture.Name, (gesture, name) => gesture with { Name = name }, "gesture", repairs);

    /// <summary>
    /// The ignored apps, an incoming one renamed when its name is taken on its own list (names are unique like group names, but
    /// per list since format 15: Global and Per command may each hold a "Blender").
    /// </summary>
    private static IgnoredApp[] Ignored(SyncItem.IgnoredItem[] items, IReadOnlySet<SyncItemKey> incoming, List<SyncRepair> repairs)
        => NamesUnique(items, [.. items.Select(item => item.App)], incoming, app => app.Name, (app, name) => app with { Name = name }, "excluded app", repairs, app => "on " + MappingRules.ListName(app));

    /// <summary>
    /// <paramref name="values"/> (one per item, in place) with names unique in one scope, or in one scope per <paramref name="whereOf"/>
    /// (the excluded apps' list, "on Global"; null for one scope), the incoming ones renamed (<see cref="Claimed"/>).
    /// </summary>
    private static T[] NamesUnique<T>(IReadOnlyList<SyncItem> items, T[] values, IReadOnlySet<SyncItemKey> incoming, Func<T, string> nameOf, Func<T, string, T> withName, string noun, List<SyncRepair> repairs, Func<T, string>? whereOf = null)
    {
        var scopes = new Dictionary<string, NameScope>();
        foreach (int index in IncomingLast(items, incoming))
        {
            var where = whereOf?.Invoke(values[index]);
            if (!scopes.TryGetValue(where ?? string.Empty, out var names))
            {
                scopes[where ?? string.Empty] = names = new NameScope();
            }

            var name = Claimed(names, nameOf(values[index]), items[index].Key, noun, where, repairs);
            if (name != nameOf(values[index]))
            {
                values[index] = withName(values[index], name);
            }
        }

        return values;
    }

    /// <summary>The group headers, Global first (restored if the merge lost it) and never renamed.</summary>
    private static List<AppGroup> Groups(SyncItem.GroupItem[] items, IReadOnlySet<SyncItemKey> incoming, List<SyncRepair> repairs)
    {
        var groups = items.Select(item => item.Header).ToList();
        var keys = items.Select(item => item.Key).ToList();
        if (!groups.Any(group => group.IsGlobal))
        {
            groups.Insert(0, AppGroup.EmptyGlobal);
            keys.Insert(0, SyncItemKey.ForGroup(GroupId.Global));
        }

        var names = new NameScope();
        var order = Enumerable.Range(0, groups.Count)
            .OrderBy(index => groups[index].IsGlobal ? 0 : incoming.Contains(keys[index]) ? 2 : 1);
        foreach (int index in order)
        {
            var name = Claimed(names, groups[index].Name, keys[index], "app group", null, repairs);
            if (name != groups[index].Name)
            {
                groups[index] = groups[index] with { Name = name };
            }
        }

        return groups;
    }

    private static Dictionary<GroupId, List<CommandCategory>> Categories(
        SyncItem.CategoryItem[] items,
        IReadOnlySet<SyncItemKey> incoming,
        List<AppGroup> groups,
        List<SyncRepair> repairs)
    {
        var byGroup = groups.ToDictionary(group => group.Id, _ => new List<CommandCategory>());
        var groupNames = groups.ToDictionary(group => group.Id, group => group.Name);
        var names = groups.ToDictionary(group => group.Id, _ => new NameScope());
        foreach (int index in IncomingLast(items, incoming))
        {
            var item = items[index];
            if (!byGroup.TryGetValue(item.GroupId, out var list))
            {
                repairs.Add(new(item.Key, SyncRepairKind.CategoryDropped, $"Category '{item.Category.Name}' dropped: its app group is gone."));
                continue;
            }

            var name = Claimed(names[item.GroupId], item.Category.Name, item.Key, "category", $"in '{groupNames[item.GroupId]}'", repairs);
            list.Add(item.Category with { Name = name });
        }

        return byGroup;
    }

    /// <summary>Each own version by its command; one whose command did not survive the merge is dropped with a repair line.</summary>
    private static Dictionary<CommandId, CommandVersion> Versions(SyncItem.VersionItem[] items, Dictionary<GroupId, List<Command>> commands, List<SyncRepair> repairs)
    {
        var present = commands.Values.SelectMany(list => list).Select(command => command.Id).ToHashSet();
        var versions = new Dictionary<CommandId, CommandVersion>();
        foreach (var item in items)
        {
            if (present.Contains(item.CommandId))
            {
                versions[item.CommandId] = item.Version;
            }
            else
            {
                repairs.Add(new(item.Key, SyncRepairKind.OwnStepsDropped, $"{item.Name} dropped: the command is gone."));
            }
        }

        return versions;
    }

    /// <summary>
    /// An own version's trigger (F8, 2026-10-09) arrives with its own-steps item, after the commands were placed: one naming a
    /// gesture that is gone, or overlapping another command of the group, leaves that platform unbound, with a repair line.
    /// </summary>
    private static Command[] OwnTriggersChecked(Command[] commands, AppGroup group, HashSet<GestureId> gestures, List<SyncRepair> repairs)
    {
        for (var i = 0; i < commands.Length; i++)
        {
            if (commands[i].OwnVersion is not { Trigger: { IsBound: true } trigger } own)
            {
                continue;
            }

            var key = SyncItemKey.ForCommandVersion(commands[i].Id);
            string? problem = trigger is Trigger.GestureTrigger gesture && !gestures.Contains(gesture.GestureId)
                ? "its gesture is gone"
                : commands.Where((other, index) => index != i).Select(other => MappingRules.Overlap(commands[i], other)).FirstOrDefault(clash => clash is not null) is { } clash
                    ? $"another command already uses {clash}"
                    : null;
            if (problem is not null)
            {
                repairs.Add(new(key, SyncRepairKind.TriggerCleared, $"The own trigger of '{commands[i].Name}' {CommandNames.Where(group, commands[i])} unbound: {problem}."));
                commands[i] = commands[i] with { OwnVersion = own with { Trigger = Trigger.None } };
            }
        }

        return commands;
    }

    /// <summary>What <see cref="Build"/> returns.</summary>
    internal sealed record Built(IReadOnlyList<Gesture> Gestures, MappingDocument Mapping, SyncItemSet Items, IReadOnlyList<SyncRepair> Repairs);
}
