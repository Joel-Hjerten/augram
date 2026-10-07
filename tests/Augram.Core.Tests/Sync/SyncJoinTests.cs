using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Sync;

/// <summary>The first sync of a machine against a repo that already holds another machine's file.</summary>
public sealed class SyncJoinTests : TwoMachineTest
{
    [Fact]
    public void TheFirstMachinePublishesItsFileAndChangesNothing()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var before = work.Items;

        var report = work.Sync();

        Assert.Equal(SyncStatus.UpToDate, report.Status);
        Assert.True(report.Counts.IsEmpty);
        Assert.Equal(["sync from PC-WORK"], Remote.Messages);
        Assert.True(Remote.Files.ContainsKey(work.Id.ToString("D")));
        Assert.Equal(SyncMachine.Url, work.Repository.PreparedUrl);
        Assert.True(work.Items.SameAs(before.Contents()));
    }

    [Fact]
    public void AJoinerIsAskedFirstAndNothingChanges()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var home = Machine("PC-HOME", ([SyncSamples.NewGesture("Mine")], MappingDocument.Empty));
        work.Sync();

        var report = home.Sync();

        Assert.Equal(SyncStatus.NeedsJoinChoice, report.Status);
        var other = Assert.Single(report.OtherMachines);
        Assert.Equal(work.Id, other.MachineId);
        Assert.Equal("PC-WORK", other.MachineName);
        Assert.Equal((3, 2, 3), (other.Gestures, other.Groups, other.Commands));
        Assert.Equal("Mine", Assert.Single(home.Gestures.All).Name);
        Assert.True(home.Bases.IsEmpty);
        Assert.Single(Remote.Files);
    }

    [Fact]
    public void UsingTheSyncedSettingsReplacesEverythingHere()
    {
        var (work, home) = Joined();

        Assert.True(home.SameAs(work));
        Assert.DoesNotContain(home.Gestures.All, gesture => gesture.Name == "Mine");
        Assert.Equal(2, Remote.Files.Count);
        Assert.Equal(SyncStatus.UpToDate, work.Sync().Status);
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void ApplyingASyncIsOneUndoStepPerStore()
    {
        var (_, home) = Joined();

        Assert.True(home.Gestures.Undo());
        Assert.True(home.Mapping.Undo());

        Assert.Equal("Mine", Assert.Single(home.Gestures.All).Name);
        Assert.Empty(home.Mapping.Current.AllCommands());
        Assert.False(home.Gestures.CanUndo);
        Assert.False(home.Mapping.CanUndo);
    }

    [Fact]
    public void JoiningWithMergeKeepsBothSetsAndRenamesClashes()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var home = Machine("PC-HOME", SyncSamples.Setup());
        var workUp = work.Gesture("Up").Id;
        work.Sync();

        var report = home.Sync(SyncJoin.Merge);

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Equal(["Up", "Down", "Left", "Up (2)", "Down (2)", "Left (2)"], home.Gestures.All.Select(gesture => gesture.Name));
        Assert.Equal(["Global", "Chrome", "Chrome (2)"], home.Mapping.Current.Groups.Select(group => group.Name));
        Assert.Equal(["Close", "Close (2)", "Minimize", "Minimize (2)"], home.Mapping.Current.Global.Commands.Select(command => command.Name));
        Assert.Equal(["Window", "Window (2)"], home.Mapping.Current.Global.Categories.Select(category => category.Name));
        Assert.Equal(2, home.Mapping.Current.Ignored.Count);
        Assert.Contains(report.Repairs, repair => repair.Key == SyncItemKey.ForGesture(workUp) && repair.Kind == SyncRepairKind.Renamed);
        Assert.Empty(report.Conflicts);

        work.Sync();

        Assert.True(work.SameAs(home));
        Assert.Equal("Up (2)", work.Gestures.Find(workUp)!.Name);
    }

    [Fact]
    public void UsingTheSyncedSettingsTakesTheNewestOtherMachine()
    {
        var older = Machine("PC-OLD", SyncSamples.Setup());
        var newer = Machine("PC-NEW", ([SyncSamples.NewGesture("Fresh")], MappingDocument.Empty));
        older.Sync();
        newer.Sync(SyncJoin.Merge);
        var home = Machine("PC-HOME");

        home.Sync(SyncJoin.UseRemote);

        Assert.True(home.SameAs(newer));
    }
}
