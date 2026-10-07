using Augram.Core.Mapping;
using Augram.Core.Sync;
using Xunit;
using static Augram.Core.Tests.Sync.Support.MergeScenario;

namespace Augram.Core.Tests.Sync;

/// <summary>The merge table (Sync/README.md), one test per row, each for every item kind.</summary>
public sealed class ThreeWayMergeTests
{
    public static TheoryData<SyncItemKind> AllKinds => Kinds;

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void UnchangedEverywhereStaysAsItIs(SyncItemKind kind)
        => AssertRow(kind, @base: 0, local: 0, remote: 0, expected: 0);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ChangedHereOnlyKeepsMine(SyncItemKind kind)
        => AssertRow(kind, @base: 0, local: 1, remote: 0, expected: 1);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ChangedThereOnlyTakesTheirs(SyncItemKind kind)
    {
        var result = AssertRow(kind, @base: 0, local: 0, remote: 1, expected: 1);

        Assert.Equal(new SyncKindCounts(0, 1, 0), result.Counts.For(kind));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ChangedAlikeOnBothSidesIsNoConflict(SyncItemKind kind)
        => AssertRow(kind, @base: 0, local: 1, remote: 1, expected: 1);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ChangedDifferentlyOnBothSidesIsAConflictThatKeepsMine(SyncItemKind kind)
    {
        var result = AssertRow(kind, @base: 0, local: 1, remote: 2, expected: 1, conflict: true);

        Assert.Equal(Name(1), Assert.Single(result.Conflicts).Name);
        Assert.True(result.Counts.IsEmpty);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void AddedHereIsKept(SyncItemKind kind)
        => AssertRow(kind, @base: null, local: 1, remote: null, expected: 1);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void AddedThereIsTaken(SyncItemKind kind)
    {
        var result = AssertRow(kind, @base: null, local: null, remote: 1, expected: 1);

        Assert.Equal(new SyncKindCounts(1, 0, 0), result.Counts.For(kind));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void AddedAlikeOnBothSidesIsOne(SyncItemKind kind)
    {
        var result = AssertRow(kind, @base: null, local: 1, remote: 1, expected: 1);

        Assert.Single(result.Items, item => item.Key == Key(kind));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void AddedDifferentlyOnBothSidesIsAConflictThatKeepsMine(SyncItemKind kind)
        => AssertRow(kind, @base: null, local: 1, remote: 2, expected: 1, conflict: true);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DeletedHereAndUnchangedThereStaysDeleted(SyncItemKind kind)
        => AssertRow(kind, @base: 0, local: null, remote: 0, expected: null);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DeletedThereAndUnchangedHereIsDeleted(SyncItemKind kind)
    {
        var result = AssertRow(kind, @base: 0, local: 0, remote: null, expected: null);

        Assert.Equal(new SyncKindCounts(0, 0, 1), result.Counts.For(kind));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DeletedOnBothSidesIsGone(SyncItemKind kind)
        => AssertRow(kind, @base: 0, local: null, remote: null, expected: null);

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DeletedHereAndChangedThereIsAConflictThatStaysDeleted(SyncItemKind kind)
    {
        var conflict = Assert.Single(AssertRow(kind, @base: 0, local: null, remote: 1, expected: null, conflict: true).Conflicts);

        Assert.True(conflict.DeletedHere);
        Assert.False(conflict.DeletedThere);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void DeletedThereAndChangedHereIsAConflictThatKeepsMine(SyncItemKind kind)
    {
        var conflict = Assert.Single(AssertRow(kind, @base: 0, local: 1, remote: null, expected: 1, conflict: true).Conflicts);

        Assert.True(conflict.DeletedThere);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void AHeldKeyKeepsMineAndIsNoConflict(SyncItemKind kind)
    {
        var result = ThreeWayMerge.Merge(Items(kind, 0).Contents(), Items(kind, 1), Items(kind, 2), new HashSet<SyncItemKey> { Key(kind) });

        Assert.Equal(Content(kind, 1), result.Items.Find(Key(kind))?.Content);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void ACommandMovedToAnotherGroupThereMovesHere()
    {
        var inChrome = Items(SyncItemKind.Command, 0);
        var moved = Moved(inChrome);

        var result = ThreeWayMerge.Merge(inChrome, inChrome, moved);

        var command = Assert.Single(result.Mapping.Global.Commands);
        Assert.Equal(ItemCommand, command.Id);
        Assert.Empty(result.Mapping.Groups.Single(group => group.Id == Chrome).Commands);
        Assert.Equal(new SyncKindCounts(0, 1, 0), result.Counts.Commands);
    }

    [Fact]
    public void ACommandMovedHereAndRenamedThereIsAConflictThatKeepsItMoved()
    {
        var inChrome = Items(SyncItemKind.Command, 0);

        var result = ThreeWayMerge.Merge(inChrome, Moved(inChrome), Items(SyncItemKind.Command, 1));

        Assert.Equal(SyncItemKind.Command, Assert.Single(result.Conflicts).Kind);
        Assert.Equal(ItemCommand, Assert.Single(result.Mapping.Global.Commands).Id);
    }

    [Fact]
    public void ACategoryMovedToAnotherGroupThereMovesHere()
    {
        var inGlobal = Items(SyncItemKind.Category, 0);
        var category = new CommandCategory(ItemCategory, Name(0));
        var groups = inGlobal.OfType<SyncItem.GroupItem>().Select(item => item.Header).ToArray();
        var moved = Set([Anchor], new MappingDocument(
            groups.Select(group => group.Id == Chrome ? group with { Categories = [category] } : group).ToArray(),
            []));

        var result = ThreeWayMerge.Merge(inGlobal, inGlobal, moved);

        Assert.Empty(result.Mapping.Global.Categories);
        Assert.Equal(ItemCategory, Assert.Single(result.Mapping.Groups.Single(group => group.Id == Chrome).Categories).Id);
        Assert.Empty(result.Conflicts);
        Assert.Equal(new SyncKindCounts(1, 0, 1), result.Counts.Categories);
    }

    [Fact]
    public void TheResultIsValidForTheStores()
    {
        var result = ThreeWayMerge.Merge(SyncItemSet.Empty, Items(SyncItemKind.Command, 1), Items(SyncItemKind.Gesture, 1));

        _ = new MappingStore(result.Mapping);
        _ = new Core.Gestures.GestureLibrary(result.Gestures);
        Assert.Equal(2, result.Gestures.Count);
        Assert.Single(result.Mapping.AllCommands());
    }

    /// <summary>The command item of <paramref name="set"/> moved from Chrome into Global.</summary>
    private static SyncItemSet Moved(SyncItemSet set)
    {
        var groups = set.OfType<SyncItem.GroupItem>().Select(item => item.Header).ToArray();
        var command = set.OfType<SyncItem.CommandItem>().Single().Command;
        return Set([Anchor], new MappingDocument(
            groups.Select(group => group.IsGlobal ? group with { Commands = [command] } : group).ToArray(),
            []));
    }

    private static SyncMergeResult AssertRow(SyncItemKind kind, int? @base, int? local, int? remote, int? expected, bool conflict = false)
    {
        var result = ThreeWayMerge.Merge(Items(kind, @base), Items(kind, local), Items(kind, remote));

        Assert.Equal(Content(kind, expected), result.Items.Find(Key(kind))?.Content);
        Assert.Empty(result.Repairs);
        if (conflict)
        {
            var found = Assert.Single(result.Conflicts);
            Assert.Equal(Key(kind), found.Key);
            Assert.Equal(kind, found.Kind);
            Assert.Equal(Content(kind, local), found.LocalContent);
            Assert.Equal(Content(kind, remote), found.RemoteContent);
        }
        else
        {
            Assert.Empty(result.Conflicts);
        }

        return result;
    }
}
