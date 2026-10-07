using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Fixtures;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>
/// The documents of the merge-table tests: a fixed anchor (a gesture, the Global group, an app group "Chrome")
/// plus one item of the kind under test, absent (null) or in a numbered variant: 0 as in the base, 1 and 2
/// two different edits of it (its name). The ids are fixed for the test run, so the variants are the same item.
/// </summary>
internal static class MergeScenario
{
    public static readonly Gesture Anchor = TestGestures.Create("Anchor", [new GesturePoint(0, 0), new GesturePoint(100, 0)]);
    public static readonly GroupId Chrome = GroupId.New();
    public static readonly GestureId ItemGesture = GestureId.New();
    public static readonly GroupId ItemGroup = GroupId.New();
    public static readonly CategoryId ItemCategory = CategoryId.New();
    public static readonly CommandId ItemCommand = CommandId.New();
    public static readonly GroupId ItemIgnored = GroupId.New();

    public static TheoryData<SyncItemKind> Kinds =>
    [
        SyncItemKind.Gesture,
        SyncItemKind.Group,
        SyncItemKind.Category,
        SyncItemKind.Command,
        SyncItemKind.Ignored,
    ];

    public static SyncItemKey Key(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => SyncItemKey.ForGesture(ItemGesture),
        SyncItemKind.Group => SyncItemKey.ForGroup(ItemGroup),
        SyncItemKind.Category => SyncItemKey.ForCategory(GroupId.Global, ItemCategory),
        SyncItemKind.Command => SyncItemKey.ForCommand(ItemCommand),
        _ => SyncItemKey.ForIgnored(ItemIgnored),
    };

    public static string Name(int variant) => variant switch
    {
        0 => "Item",
        1 => "Item one",
        _ => "Item two",
    };

    public static SyncItemSet Items(SyncItemKind kind, int? variant)
    {
        var gestures = new List<Gesture> { Anchor };
        var global = AppGroup.EmptyGlobal;
        var chrome = new AppGroup(Chrome, "Chrome", IsActive: true, SuppressGlobals: false, ByProcess("chrome.exe"), []);
        var extraGroups = new List<AppGroup>();
        var ignored = new List<IgnoredApp>();
        if (variant is { } v)
        {
            var name = Name(v);
            switch (kind)
            {
                case SyncItemKind.Gesture:
                    gestures.Add(new Gesture(ItemGesture, name, IsActive: true, Anchor.Samples));
                    break;
                case SyncItemKind.Group:
                    extraGroups.Add(new AppGroup(ItemGroup, name, IsActive: true, SuppressGlobals: false, ByProcess("item.exe"), []));
                    break;
                case SyncItemKind.Category:
                    global = global with { Categories = [new CommandCategory(ItemCategory, name)] };
                    break;
                case SyncItemKind.Command:
                    chrome = chrome with { Commands = [new Command(ItemCommand, name, Trigger.ForGesture(Anchor.Id), IsActive: true, [NewStep(name)])] };
                    break;
                default:
                    ignored.Add(new IgnoredApp(ItemIgnored, name, IsActive: true, ByProcess("item.exe"), DisableEntirely: false));
                    break;
            }
        }

        return Set(gestures, new MappingDocument([global, chrome, .. extraGroups], ignored));
    }

    /// <summary>The item's content in that variant; null when absent.</summary>
    public static string? Content(SyncItemKind kind, int? variant) => Items(kind, variant).Find(Key(kind))?.Content;

    public static SyncItemSet Set(IEnumerable<Gesture> gestures, MappingDocument mapping)
        => SyncItemSet.From(GestureRules.ValidSet(gestures), MappingRules.ValidDocument(mapping));
}
