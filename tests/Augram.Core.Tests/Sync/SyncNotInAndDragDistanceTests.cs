using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// A command's own drag distance, its "Not in" and the Ignored › Per command entries it names in the sync (sync formats 12 and
/// 13, plan 0004): the distance and the "Not in" travel in the command item, the entry's scope in the ignored app item; a "Not
/// in" naming an entry the merge removed (or moved to Ignored › Global) is dropped when the merged document is validated,
/// never a conflict or a failed merge.
/// </summary>
public sealed class SyncNotInAndDragDistanceTests : TwoMachineTest
{
    private static readonly Gesture Anchor = SyncSamples.NewGesture("Anchor");
    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right));

    /// <summary>Right + wheel down handed back as a drag after <paramref name="distance"/> px.</summary>
    private static Trigger RightWheelDownAt(int distance) => Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: distance));

    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    [Fact]
    public void ACommandItemCarriesItsDragDistanceAndNotIn()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] };

        var item = new SyncItem.CommandItem(GroupId.Global, zoom);

        Assert.Contains("\"hold\":{\"buttons\":\"Right\",\"dragDistancePx\":3}", item.Content, StringComparison.Ordinal);
        Assert.Contains($"\"notIn\":[\"{spine.Id}\"]", item.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.CommandItem>(SyncItem.Parse(item.Key, item.Content, FakeStepType.Registry));
        Assert.Equal(zoom.Trigger, back.Command.Trigger);
        Assert.Equal(zoom.NotIn, back.Command.NotIn);
        Assert.Equal(item.Content, back.Content);
    }

    [Fact]
    public void AnIgnoredAppItemCarriesItsScope_AGlobalEntryReadsAsBefore()
    {
        var spine = PerCommand("Spine");
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true);

        var item = new SyncItem.IgnoredItem(spine);
        var global = new SyncItem.IgnoredItem(game);

        Assert.Contains("\"name\":\"Spine\",\"scope\":\"PerCommand\",\"isActive\":true", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("scope", global.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.IgnoredItem>(SyncItem.Parse(item.Key, item.Content, FakeStepType.Registry));
        Assert.Equal((spine.Id, IgnoreScope.PerCommand), (back.App.Id, back.App.Scope));
        Assert.Equal(item.Content, back.Content);
        Assert.Equal(IgnoreScope.Global, Assert.IsType<SyncItem.IgnoredItem>(SyncItem.Parse(global.Key, global.Content, FakeStepType.Registry)).App.Scope);
    }

    /// <summary>One side deletes Spine, the other ticks it in Zoom In's "Not in": the deletion stands and the "Not in" drops it, either way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANotInNamingAnEntryDeletedOnTheOtherSide_IsDroppedByTheMerge_WithoutAConflict(bool deletedHere)
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom"));
        var both = Document([NewGlobal(zoom)], spine);
        var deleted = Document([NewGlobal(zoom)]);
        var ticked = Document([NewGlobal(zoom with { NotIn = [spine.Id] })], spine);

        var result = deletedHere
            ? ThreeWayMerge.Merge(Items(both), Items(deleted), Items(ticked))
            : ThreeWayMerge.Merge(Items(both), Items(ticked), Items(deleted));

        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Mapping.Ignored);
        Assert.Empty(result.Mapping.Global.FindCommand(zoom.Id)!.NotIn);
    }

    /// <summary>One side moves Spine to Ignored › Global, the other ticks it: the move stands and the "Not in" drops it.</summary>
    [Fact]
    public void ANotInNamingAnEntryMovedToGlobalOnTheOtherSide_IsDroppedByTheMerge_WithoutAConflict()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom"));
        var both = Document([NewGlobal(zoom)], spine);
        var moved = Document([NewGlobal(zoom)], spine with { Scope = IgnoreScope.Global });
        var ticked = Document([NewGlobal(zoom with { NotIn = [spine.Id] })], spine);

        var result = ThreeWayMerge.Merge(Items(both), Items(moved), Items(ticked));

        Assert.Empty(result.Conflicts);
        Assert.Equal(IgnoreScope.Global, result.Mapping.Ignored.Single().Scope);
        Assert.Empty(result.Mapping.Global.FindCommand(zoom.Id)!.NotIn);
    }

    [Fact]
    public void ADragDistanceANotInAndAPerCommandEntryMadeOnOneMachine_ArriveOnTheOther()
    {
        var (work, home) = Joined();
        var spine = work.Mapping.AddIgnored(PerCommand("Spine"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] });
        var chrome = work.Mapping.Current.Groups.Single(group => group.Name == "Chrome");
        work.Mapping.AddCommand(chrome.Id, NewCommand("Chrome zoom", RightWheelDown, NewStep("chrome")) with { NotIn = [spine.Id] });
        work.Sync();

        Assert.Equal(SyncStatus.Applied, home.Sync().Status);

        var arrived = home.Command("Zoom In");
        Assert.Equal(3, arrived.Trigger.Hold.DragDistancePx);
        Assert.Equal([spine.Id], arrived.NotIn);
        Assert.Equal([spine.Id], home.Command("Chrome zoom").NotIn);
        Assert.Equal(IgnoreScope.PerCommand, home.Mapping.FindIgnored(spine.Id)!.Scope);
        Assert.True(work.SameAs(home));

        home.Mapping.UpdateCommand(GroupId.Global, arrived with { Trigger = RightWheelDownAt(5), NotIn = [] });
        home.Sync();
        work.Sync();

        Assert.Equal((5, 0), (work.Command("Zoom In").Trigger.Hold.DragDistancePx, work.Command("Zoom In").NotIn.Count));
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void SpineDeletedOnOneMachineAndTickedOnTheOther_BothEndWithoutIt()
    {
        var (work, home) = Joined();
        var spine = work.Mapping.AddIgnored(PerCommand("Spine"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDown, NewStep("zoom")));
        work.Sync();
        home.Sync();

        work.Mapping.RemoveIgnored(spine.Id);
        home.Mapping.UpdateCommand(GroupId.Global, home.Command("Zoom In") with { NotIn = [spine.Id] });
        Assert.Empty(work.Sync().Conflicts);
        var report = home.Sync();

        Assert.Empty(report.Conflicts);
        Assert.Null(home.Mapping.FindIgnored(spine.Id));
        Assert.Empty(home.Command("Zoom In").NotIn);
        work.Sync();
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void AFileWithAPerCommandEntryIsFormatThirteenOrLater_ABuildThatReadsUpToTwelvePausesOnIt()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var spine = work.Mapping.AddIgnored(PerCommand("Spine"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] });
        work.Sync();
        var file = Remote.Files[work.Id.ToString("D")];

        var header = SyncFileSerializer.ReadHeader(file)!;

        Assert.True(SyncFile.CurrentFormatVersion >= 13);
        Assert.Contains("\"dragDistancePx\": 3", file, StringComparison.Ordinal);
        Assert.Contains("\"notIn\": [", file, StringComparison.Ordinal);
        Assert.Contains("\"scope\": \"PerCommand\"", file, StringComparison.Ordinal);
        Assert.True(header.IsNewerThan(schemaVersion: 5, formatVersion: 12));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 12));
        Assert.False(header.IsNewer);
    }

    private static MappingDocument Document(AppGroup[] groups, params IgnoredApp[] ignored) => new(groups, ignored);

    private static SyncItemSet Items(MappingDocument mapping) => MergeScenario.Set([Anchor], mapping);
}
