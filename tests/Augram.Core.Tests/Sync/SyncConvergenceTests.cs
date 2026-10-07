using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>Two joined machines editing and syncing: changes travel, deletions travel, conflicts wait for a choice.</summary>
public sealed class SyncConvergenceTests : TwoMachineTest
{
    [Fact]
    public void ChangesOnBothSidesConverge()
    {
        var (work, home) = Joined();
        var left = work.Gesture("Left").Id;
        work.Gestures.Rename(left, "West");
        home.Mapping.AddCommand(GroupId.Global, NewCommand("Back", left, NewStep("alt+left")));

        Assert.True(work.Sync().Counts.IsEmpty);
        var atHome = home.Sync();
        var atWork = work.Sync();

        Assert.Equal(new SyncKindCounts(0, 1, 0), atHome.Counts.Gestures);
        Assert.Equal(new SyncKindCounts(1, 0, 0), atWork.Counts.Commands);
        Assert.Equal(SyncStatus.Applied, atWork.Status);
        Assert.True(work.SameAs(home));
        Assert.Equal("West", home.Gestures.Find(left)!.Name);
        Assert.Equal(SyncStatus.UpToDate, home.Sync().Status);
    }

    [Fact]
    public void ADeletionTravels()
    {
        var (work, home) = Joined();
        work.Mapping.RemoveCommand(work.Command("Close").Id);
        work.Sync();

        var report = home.Sync();

        Assert.Equal(new SyncKindCounts(0, 0, 1), report.Counts.Commands);
        Assert.DoesNotContain(home.Mapping.Current.AllCommands(), pair => pair.Command.Name == "Close");
        Assert.True(home.SameAs(work));
    }

    [Fact]
    public void ADeletionHereIsNotUndoneByAMachineThatHasNotSyncedSince()
    {
        var (work, home) = Joined();
        home.Mapping.RemoveIgnored(home.Mapping.Current.Ignored[0].Id);

        home.Sync();
        home.Sync();

        Assert.Empty(home.Mapping.Current.Ignored);
        work.Sync();
        Assert.Empty(work.Mapping.Current.Ignored);
    }

    [Fact]
    public void AMachineThatPublishedWithoutSeeingTheNewestFileDoesNotRevertIt()
    {
        var (work, home) = Joined();
        work.Sync();
        var workFile = work.Id.ToString("D");
        var before = Remote.Files[workFile];
        var left = work.Gesture("Left").Id;
        work.Gestures.Rename(left, "West");
        work.Sync();
        var after = Remote.Files[workFile];

        Remote.Files[workFile] = before;
        home.Mapping.AddCommand(GroupId.Global, NewCommand("Back", left, NewStep("alt+left")));
        home.Sync();
        Remote.Files[workFile] = after;
        work.Sync();

        Assert.Equal("West", work.Gestures.Find(left)!.Name);
        Assert.Contains(work.Mapping.Current.Global.Commands, command => command.Name == "Back");
        home.Sync();
        Assert.True(home.SameAs(work));
    }

    [Fact]
    public void ThreeMachinesConverge()
    {
        var (work, home) = Joined();
        var laptop = Machine("LAPTOP");
        Assert.Equal(SyncStatus.Applied, laptop.Sync(SyncJoin.UseRemote).Status);
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        home.Mapping.RemoveCommand(home.Command("Close tab").Id);
        laptop.Mapping.AddCommand(GroupId.Global, NewCommand("Lock", laptop.Gesture("Left").Id, NewStep("win+l")));

        foreach (var machine in new[] { work, home, laptop, work, home, laptop })
        {
            Assert.Empty(machine.Sync().Conflicts);
        }

        Assert.True(work.SameAs(home));
        Assert.True(home.SameAs(laptop));
        Assert.Contains(laptop.Gestures.All, gesture => gesture.Name == "North");
        Assert.DoesNotContain(work.Mapping.Current.AllCommands(), pair => pair.Command.Name == "Close tab");
        Assert.Contains(home.Mapping.Current.Global.Commands, command => command.Name == "Lock");
    }

    [Fact]
    public void ChangingTheSameCommandOnBothSidesIsAConflictOnTheSecondToSync()
    {
        var (work, home, conflict) = Conflicted();
        var id = work.Command("Close window").Id;

        Assert.Equal(SyncItemKey.ForCommand(id), conflict.Key);
        Assert.Equal(SyncItemKind.Command, conflict.Kind);
        Assert.Equal("Close app", conflict.Name);
        Assert.Equal(work.Id, conflict.MachineId);
        Assert.Equal("PC-WORK", conflict.MachineName);
        Assert.Contains("Close app", conflict.LocalContent, StringComparison.Ordinal);
        Assert.Contains("Close window", conflict.RemoteContent, StringComparison.Ordinal);
        Assert.Equal("Close app", home.Mapping.Current.Global.FindCommand(id)!.Name);
        Assert.Equal("Close window", work.Mapping.Current.Global.FindCommand(id)!.Name);
        Assert.Single(home.Coordinator.PendingConflicts());
        Assert.Empty(work.Coordinator.PendingConflicts());

        Assert.Single(home.Sync().Conflicts);
        Assert.Equal("Close window", work.Mapping.Current.Global.FindCommand(id)!.Name);
    }

    [Fact]
    public void TakingTheirsConverges()
    {
        var (work, home, conflict) = Conflicted();

        var resolved = home.Resolve(conflict, SyncChoice.TakeTheirs);

        Assert.Equal(SyncStatus.Applied, resolved.Status);
        Assert.Empty(resolved.Conflicts);
        Assert.Equal("Close window", home.Mapping.Current.Global.FindCommand(new CommandId(conflict.Key.Id))!.Name);
        home.Sync();
        work.Sync();
        Assert.True(work.SameAs(home));
        Assert.Empty(home.Sync().Conflicts);
        Assert.Empty(work.Coordinator.PendingConflicts());
    }

    [Fact]
    public void KeepingMineIsTakenByTheOtherSideOnItsNextSync()
    {
        var (work, home, conflict) = Conflicted();

        var resolved = home.Resolve(conflict, SyncChoice.KeepMine);
        home.Sync();
        var atWork = work.Sync();

        Assert.Equal(SyncStatus.UpToDate, resolved.Status);
        Assert.Equal(new SyncKindCounts(0, 1, 0), atWork.Counts.Commands);
        Assert.Equal("Close app", work.Mapping.Current.Global.FindCommand(new CommandId(conflict.Key.Id))!.Name);
        Assert.True(work.SameAs(home));
        Assert.Empty(home.Sync().Conflicts);
        Assert.Empty(work.Sync().Conflicts);
    }

    [Fact]
    public void KeepingMineBeforeTheOtherSideSyncedAgainIsTakenToo()
    {
        var (work, home) = Joined();
        work.Mapping.UpdateCommand(GroupId.Global, work.Command("Close") with { Name = "Close window" });
        home.Mapping.UpdateCommand(GroupId.Global, home.Command("Close") with { Name = "Close app" });
        work.Sync();
        var conflict = Assert.Single(home.Sync().Conflicts);

        home.Resolve(conflict, SyncChoice.KeepMine);
        home.Sync();
        work.Sync();

        Assert.True(work.SameAs(home));
        Assert.Equal("Close app", work.Mapping.Current.Global.FindCommand(new CommandId(conflict.Key.Id))!.Name);
    }

    [Fact]
    public void KeepingBothAddsTheirCopyUnboundBesideMine()
    {
        var (work, home, conflict) = Conflicted();
        var id = new CommandId(conflict.Key.Id);

        var resolved = home.Resolve(conflict, SyncChoice.KeepBoth);

        var copy = home.Mapping.Current.Global.Commands.Single(command => command.Name == "Close window");
        Assert.NotEqual(id, copy.Id);
        Assert.Equal(Trigger.None, copy.Trigger);
        Assert.Equal("Close app", home.Mapping.Current.Global.FindCommand(id)!.Name);
        Assert.Contains(resolved.Repairs, repair => repair.Kind == SyncRepairKind.Unbound);
        home.Sync();
        work.Sync();
        Assert.True(work.SameAs(home));
        Assert.Empty(home.Sync().Conflicts);
    }

    [Fact]
    public void KeepingBothOfAGroupTakesTheirsAndSaysSo()
    {
        var (work, home) = Joined();
        var chrome = work.Mapping.Current.Groups.Single(group => group.Name == "Chrome");
        work.Mapping.UpdateGroup(chrome with { Name = "Chromium" });
        home.Mapping.UpdateGroup(home.Mapping.FindGroup(chrome.Id)! with { Name = "Google Chrome" });
        work.Sync();
        var conflict = Assert.Single(home.Sync().Conflicts);

        var resolved = home.Resolve(conflict, SyncChoice.KeepBoth);

        Assert.Equal("Chromium", home.Mapping.FindGroup(chrome.Id)!.Name);
        Assert.Contains("Keep both is for gestures and commands", Assert.Single(resolved.Notes), StringComparison.Ordinal);
    }

    [Fact]
    public void AConflictResolvedTwiceSaysItIsNoLongerPending()
    {
        var (_, home, conflict) = Conflicted();
        home.Resolve(conflict, SyncChoice.KeepMine);

        var again = home.Resolve(conflict, SyncChoice.TakeTheirs);

        Assert.Equal(SyncStatus.UpToDate, again.Status);
        Assert.Contains("no longer pending", Assert.Single(again.Notes), StringComparison.Ordinal);
        Assert.Equal("Close app", home.Command("Close app").Name);
    }
}
