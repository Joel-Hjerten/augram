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
/// A command's own drag distance and a Global command's "Not in" in the sync (sync format 12, plan 0004): both travel in the
/// command item; a "Not in" naming an app group the merge removed is dropped when the merged document is validated, never a
/// conflict or a failed merge.
/// </summary>
public sealed class SyncNotInAndDragDistanceTests : TwoMachineTest
{
    private static readonly Gesture Anchor = SyncSamples.NewGesture("Anchor");
    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right));

    /// <summary>Right + wheel down handed back as a drag after <paramref name="distance"/> px.</summary>
    private static Trigger RightWheelDownAt(int distance) => Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: distance));

    [Fact]
    public void ACommandItemCarriesItsDragDistanceAndNotIn()
    {
        var spine = NewGroup("Spine");
        var zoom = NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] };

        var item = new SyncItem.CommandItem(GroupId.Global, zoom);

        Assert.Contains("\"hold\":{\"buttons\":\"Right\",\"dragDistancePx\":3}", item.Content, StringComparison.Ordinal);
        Assert.Contains($"\"notIn\":[\"{spine.Id}\"]", item.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.CommandItem>(SyncItem.Parse(item.Key, item.Content, FakeStepType.Registry));
        Assert.Equal(zoom.Trigger, back.Command.Trigger);
        Assert.Equal(zoom.NotIn, back.Command.NotIn);
        Assert.Equal(item.Content, back.Content);
    }

    /// <summary>One side deletes Spine, the other ticks it in Zoom In's "Not in": the deletion stands and the "Not in" drops it, either way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANotInNamingAGroupDeletedOnTheOtherSide_IsDroppedByTheMerge_WithoutAConflict(bool deletedHere)
    {
        var spine = NewGroup("Spine", ByProcess("spine.exe"));
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom"));
        var both = Document(NewGlobal(zoom), spine);
        var deleted = Document(NewGlobal(zoom));
        var ticked = Document(NewGlobal(zoom with { NotIn = [spine.Id] }), spine);

        var result = deletedHere
            ? ThreeWayMerge.Merge(Items(both), Items(deleted), Items(ticked))
            : ThreeWayMerge.Merge(Items(both), Items(ticked), Items(deleted));

        Assert.Empty(result.Conflicts);
        Assert.DoesNotContain(result.Mapping.Groups, group => group.Id == spine.Id);
        Assert.Empty(result.Mapping.Global.FindCommand(zoom.Id)!.NotIn);
    }

    [Fact]
    public void ADragDistanceAndANotInMadeOnOneMachine_ArriveOnTheOther()
    {
        var (work, home) = Joined();
        var spine = work.Mapping.AddGroup(NewGroup("Spine", ByProcess("spine.exe")));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] });
        work.Sync();

        Assert.Equal(SyncStatus.Applied, home.Sync().Status);

        var arrived = home.Command("Zoom In");
        Assert.Equal(3, arrived.Trigger.Hold.DragDistancePx);
        Assert.Equal([spine.Id], arrived.NotIn);
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
        var spine = work.Mapping.AddGroup(NewGroup("Spine", ByProcess("spine.exe")));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDown, NewStep("zoom")));
        work.Sync();
        home.Sync();

        work.Mapping.RemoveGroup(spine.Id);
        home.Mapping.UpdateCommand(GroupId.Global, home.Command("Zoom In") with { NotIn = [spine.Id] });
        Assert.Empty(work.Sync().Conflicts);
        var report = home.Sync();

        Assert.Empty(report.Conflicts);
        Assert.Null(home.Mapping.FindGroup(spine.Id));
        Assert.Empty(home.Command("Zoom In").NotIn);
        work.Sync();
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void AFileWithThemIsFormatTwelveOrLater_ABuildThatReadsUpToElevenPausesOnIt()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var spine = work.Mapping.AddGroup(NewGroup("Spine", ByProcess("spine.exe")));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Zoom In", RightWheelDownAt(3), NewStep("zoom")) with { NotIn = [spine.Id] });
        work.Sync();
        var file = Remote.Files[work.Id.ToString("D")];

        var header = SyncFileSerializer.ReadHeader(file)!;

        Assert.True(SyncFile.CurrentFormatVersion >= 12);
        Assert.Contains("\"dragDistancePx\": 3", file, StringComparison.Ordinal);
        Assert.Contains("\"notIn\": [", file, StringComparison.Ordinal);
        Assert.True(header.IsNewerThan(schemaVersion: 4, formatVersion: 11));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 11));
        Assert.False(header.IsNewer);
    }

    private static SyncItemSet Items(MappingDocument mapping) => MergeScenario.Set([Anchor], mapping);
}
