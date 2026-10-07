using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Sync.Support.SyncSamples;

namespace Augram.Core.Tests.Sync;

/// <summary>What a merge repairs so its result passes the rules, always at the incoming item's expense.</summary>
public sealed class SyncRepairTests
{
    private static readonly Gesture Zig = NewGesture("Zig");

    [Fact]
    public void AnIncomingGestureWithATakenNameIsRenamed()
    {
        var mine = NewGesture("Zig");
        var theirs = NewGesture("zig");

        var result = Merge(Set([mine]), Set([theirs]));

        Assert.Equal("Zig", result.Gestures.Single(gesture => gesture.Id == mine.Id).Name);
        Assert.Equal("zig (2)", result.Gestures.Single(gesture => gesture.Id == theirs.Id).Name);
        AssertRepair(result, SyncRepairKind.Renamed, SyncItemKey.ForGesture(theirs.Id), "renamed 'zig (2)'");
    }

    [Fact]
    public void ASecondClashTakesTheNextFreeSuffix()
    {
        var mine = NewGesture("Zig");
        var two = NewGesture("Zig (2)");
        var theirs = NewGesture("Zig");

        var result = Merge(Set([mine, two]), Set([theirs]));

        Assert.Equal("Zig (3)", result.Gestures.Single(gesture => gesture.Id == theirs.Id).Name);
    }

    [Fact]
    public void AnIncomingGroupWithATakenNameIsRenamed()
    {
        var mine = NewGroup("Chrome");
        var theirs = NewGroup("CHROME");

        var result = Merge(Set([], mine), Set([], theirs));

        Assert.Equal("CHROME (2)", result.Mapping.Groups.Single(group => group.Id == theirs.Id).Name);
        AssertRepair(result, SyncRepairKind.Renamed, SyncItemKey.ForGroup(theirs.Id), "app group");
    }

    [Fact]
    public void AnIncomingCategoryWithANameTakenInItsGroupIsRenamed()
    {
        var mine = NewCategory("Media");
        var theirs = NewCategory("Media");

        var result = Merge(
            Set([], NewGlobal() with { Categories = [mine] }),
            Set([], NewGlobal() with { Categories = [theirs] }));

        Assert.Equal(["Media", "Media (2)"], result.Mapping.Global.Categories.Select(category => category.Name));
        AssertRepair(result, SyncRepairKind.Renamed, SyncItemKey.ForCategory(GroupId.Global, theirs.Id), "category 'Media'");
    }

    [Fact]
    public void AnIncomingCommandWithANameTakenInItsGroupIsRenamed()
    {
        var mine = NewCommand("Close");
        var theirs = NewCommand("Close");

        var result = Merge(Set([], NewGlobal(mine)), Set([], NewGlobal(theirs)));

        Assert.Equal("Close (2)", result.Mapping.Global.FindCommand(theirs.Id)!.Name);
        AssertRepair(result, SyncRepairKind.Renamed, SyncItemKey.ForCommand(theirs.Id), "command 'Close'");
    }

    [Fact]
    public void AnIncomingCommandOnATriggerAlreadyBoundInItsGroupIsUnbound()
    {
        var mine = NewCommand("Close", Zig.Id);
        var theirs = NewCommand("Shut", Zig.Id);

        var result = Merge(Set([Zig], NewGlobal(mine)), Set([Zig], NewGlobal(theirs)));

        Assert.Equal(Trigger.ForGesture(Zig.Id), result.Mapping.Global.FindCommand(mine.Id)!.Trigger);
        Assert.Equal(Trigger.None, result.Mapping.Global.FindCommand(theirs.Id)!.Trigger);
        AssertRepair(result, SyncRepairKind.Unbound, SyncItemKey.ForCommand(theirs.Id), "'Close' already uses this gesture");
    }

    [Fact]
    public void ACommandWhoseGroupIsGoneMovesIntoGlobal()
    {
        var kept = NewCommand("Close tab");
        var added = NewCommand("Reload");
        var chrome = NewGroup("Chrome", null, kept);
        var @base = Set([], NewGlobal(), chrome);
        var local = Set([], NewGlobal(), chrome with { Commands = [kept, added] });
        var remote = Set([], NewGlobal());

        var result = ThreeWayMerge.Merge(@base, local, remote);

        Assert.Null(result.Mapping.Groups.SingleOrDefault(group => group.Id == chrome.Id));
        Assert.Equal(added.Id, Assert.Single(result.Mapping.Global.Commands).Id);
        AssertRepair(result, SyncRepairKind.MovedToGlobal, SyncItemKey.ForCommand(added.Id), "moved to Global");
    }

    [Fact]
    public void ACommandWhoseGestureIsGoneIsUnbound()
    {
        var command = NewCommand("Close", Zig.Id);
        var @base = Set([Zig], NewGlobal(command));

        var result = ThreeWayMerge.Merge(@base, @base, Set([], NewGlobal(command)));

        Assert.Empty(result.Gestures);
        Assert.Equal(Trigger.None, result.Mapping.Global.FindCommand(command.Id)!.Trigger);
        AssertRepair(result, SyncRepairKind.TriggerCleared, SyncItemKey.ForCommand(command.Id), "its gesture is gone");
    }

    [Fact]
    public void AReferenceThatWasAlreadyDanglingHereIsNotTheMergesToTouch()
    {
        var command = NewCommand("Close", GestureId.New());
        var set = Set([], NewGlobal(command));

        var result = ThreeWayMerge.Merge(set, set, set);

        Assert.Empty(result.Repairs);
        Assert.Equal(command.Trigger, result.Mapping.Global.FindCommand(command.Id)!.Trigger);
    }

    [Fact]
    public void ACommandWhoseCategoryIsGoneIsUncategorized()
    {
        var media = NewCategory("Media");
        var @base = Set([], NewGlobal() with { Categories = [media] });
        var play = NewCommand("Play").In(media);
        var local = Set([], NewGlobal(play) with { Categories = [media] });

        var result = ThreeWayMerge.Merge(@base, local, Set([], NewGlobal()));

        Assert.Empty(result.Mapping.Global.Categories);
        Assert.Null(result.Mapping.Global.FindCommand(play.Id)!.CategoryId);
        AssertRepair(result, SyncRepairKind.CategoryCleared, SyncItemKey.ForCommand(play.Id), "uncategorized");
    }

    [Fact]
    public void ACategoryWhoseGroupIsGoneIsDropped()
    {
        var chrome = NewGroup("Chrome");
        var tabs = NewCategory("Tabs");
        var @base = Set([], NewGlobal(), chrome);
        var local = Set([], NewGlobal(), chrome with { Categories = [tabs] });

        var result = ThreeWayMerge.Merge(@base, local, Set([], NewGlobal()));

        Assert.Single(result.Mapping.Groups);
        AssertRepair(result, SyncRepairKind.CategoryDropped, SyncItemKey.ForCategory(chrome.Id, tabs.Id), "dropped");
    }

    [Fact]
    public void ARepairedResultIsWhatTheStoresAccept()
    {
        var mine = NewCommand("Close", Zig.Id);
        var theirs = NewCommand("close", Zig.Id);
        var gestureTwin = NewGesture("Zig");

        var result = Merge(Set([Zig], NewGlobal(mine)), Set([gestureTwin], NewGlobal(theirs)));

        var store = new MappingStore(result.Mapping);
        var library = new GestureLibrary(result.Gestures);
        Assert.Equal(2, store.Global.Commands.Count);
        Assert.Equal(2, library.All.Count);
        Assert.Equal(3, result.Repairs.Count);
    }

    /// <summary>Nothing shared yet (an empty base): what only one side has is kept, and what only the remote has is incoming.</summary>
    private static SyncMergeResult Merge(SyncItemSet local, SyncItemSet remote) => ThreeWayMerge.Merge(SyncItemSet.Empty, local, remote);

    private static SyncItemSet Set(IEnumerable<Gesture> gestures, params AppGroup[] groups)
        => MergeScenario.Set(gestures, new MappingDocument(groups.Any(group => group.IsGlobal) ? groups : [NewGlobal(), .. groups], []));

    private static void AssertRepair(SyncMergeResult result, SyncRepairKind kind, SyncItemKey key, string text)
    {
        var repair = Assert.Single(result.Repairs, repair => repair.Kind == kind && repair.Key == key);
        Assert.Contains(text, repair.Description, StringComparison.Ordinal);
    }
}
