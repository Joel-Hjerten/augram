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
/// is gone is dropped. Gestures keep the order of <c>merged</c>.
/// </summary>
internal static class SyncDocumentBuilder
{
    public static Built Build(IReadOnlyList<SyncItem> merged, IReadOnlySet<SyncItemKey> incoming, SyncItemSet before)
    {
        var repairs = new List<SyncRepair>();
        var gestures = Gestures(merged.OfType<SyncItem.GestureItem>().ToArray(), incoming, repairs);
        var groups = Groups(merged.OfType<SyncItem.GroupItem>().ToArray(), incoming, repairs);
        var categories = Categories(merged.OfType<SyncItem.CategoryItem>().ToArray(), incoming, groups, repairs);
        var commands = new CommandPlacement(groups, categories, gestures.Select(gesture => gesture.Id).ToHashSet(), before, repairs)
            .Place(merged.OfType<SyncItem.CommandItem>(), incoming);
        var ignored = merged.OfType<SyncItem.IgnoredItem>().Select(item => item.App).ToArray();

        var document = new MappingDocument(
            groups.Select(group => group with { Categories = categories[group.Id], Commands = commands[group.Id] }).ToArray(),
            ignored);
        var validGestures = GestureRules.ValidSet(gestures);
        var validMapping = MappingRules.ValidDocument(document);
        return new Built(validGestures, validMapping, SyncItemSet.From(validGestures, validMapping), repairs);
    }

    /// <summary>Indexes of <paramref name="items"/> with the ones already here first and the incoming ones last, each in their original order.</summary>
    internal static IEnumerable<int> IncomingLast(IReadOnlyList<SyncItem> items, IReadOnlySet<SyncItemKey> incoming)
        => Enumerable.Range(0, items.Count).OrderBy(index => incoming.Contains(items[index].Key) ? 1 : 0);

    private static Gesture[] Gestures(SyncItem.GestureItem[] items, IReadOnlySet<SyncItemKey> incoming, List<SyncRepair> repairs)
    {
        var names = new SyncNames();
        var gestures = items.Select(item => item.Gesture).ToArray();
        foreach (int index in IncomingLast(items, incoming))
        {
            var name = names.Claim(gestures[index].Name);
            if (name != gestures[index].Name)
            {
                repairs.Add(new(items[index].Key, SyncRepairKind.Renamed, $"Incoming gesture '{gestures[index].Name}' renamed '{name}': the name is taken."));
                gestures[index] = gestures[index] with { Name = name };
            }
        }

        return gestures;
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

        var names = new SyncNames();
        var order = Enumerable.Range(0, groups.Count)
            .OrderBy(index => groups[index].IsGlobal ? 0 : incoming.Contains(keys[index]) ? 2 : 1);
        foreach (int index in order)
        {
            var name = names.Claim(groups[index].Name);
            if (name != groups[index].Name)
            {
                repairs.Add(new(keys[index], SyncRepairKind.Renamed, $"Incoming app group '{groups[index].Name}' renamed '{name}': the name is taken."));
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
        var names = groups.ToDictionary(group => group.Id, _ => new SyncNames());
        foreach (int index in IncomingLast(items, incoming))
        {
            var item = items[index];
            if (!byGroup.TryGetValue(item.GroupId, out var list))
            {
                repairs.Add(new(item.Key, SyncRepairKind.CategoryDropped, $"Category '{item.Category.Name}' dropped: its app group is gone."));
                continue;
            }

            var name = names[item.GroupId].Claim(item.Category.Name);
            if (name != item.Category.Name)
            {
                repairs.Add(new(item.Key, SyncRepairKind.Renamed, $"Incoming category '{item.Category.Name}' in '{groupNames[item.GroupId]}' renamed '{name}': the name is taken."));
            }

            list.Add(item.Category with { Name = name });
        }

        return byGroup;
    }

    /// <summary>What <see cref="Build"/> returns.</summary>
    internal sealed record Built(IReadOnlyList<Gesture> Gestures, MappingDocument Mapping, SyncItemSet Items, IReadOnlyList<SyncRepair> Repairs);
}
