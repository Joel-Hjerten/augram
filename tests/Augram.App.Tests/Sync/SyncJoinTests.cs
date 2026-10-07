using System.Globalization;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Sync;
using Augram.App.Tests.Sync.Support;
using Augram.App.ViewModels;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>The join question: asked after a run that needs it (through the presenter), its answer runs the join, Cancel keeps sync paused; the dialog's form and default.</summary>
public sealed class SyncJoinTests : SyncTestBase
{
    [Fact]
    public void TheStartUpRunAsksAndTheAnswerRunsTheJoin()
    {
        Work();
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        var presenter = new FakeSyncJoinPresenter { Answer = SyncJoin.Merge };
        using var prompter = new SyncPrompter(home.Service, presenter, action => action());

        home.Service.Start();
        home.WaitForRuns(2);

        var machines = Assert.Single(presenter.Asked);
        Assert.Equal("PC-WORK", Assert.Single(machines).MachineName);
        Assert.Equal(SyncTrigger.Join, home.Service.LastTrigger);
        Assert.Equal(SyncStatus.Applied, home.Service.LastReport!.Status);
        Assert.True(home.HasGesture("Mine"), "merge keeps this machine's gestures");
        Assert.True(home.HasGesture("Up"));
        Assert.False(home.Service.IsPaused);
    }

    [Fact]
    public void UsingTheSyncedSettingsReplacesThisMachinesGestures()
    {
        Work();
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        using var prompter = new SyncPrompter(home.Service, new FakeSyncJoinPresenter(), action => action());

        home.Service.Start();
        home.WaitForRuns(2);

        Assert.False(home.HasGesture("Mine"));
        Assert.True(home.HasGesture("Up"));
        Assert.True(home.HasGesture("Down"));
    }

    [Fact]
    public void CancelKeepsSyncPausedAndSyncNowAsksAgain()
    {
        Work();
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        var presenter = new FakeSyncJoinPresenter { Answer = null };
        using var prompter = new SyncPrompter(home.Service, presenter, action => action());

        home.Service.Start();
        home.WaitForRuns(1);
        SyncMachine.WaitFor(() => presenter.Asked.Count == 1, "the join question");
        Thread.Sleep(200);
        Assert.Equal(1, home.Service.RunCount);
        Assert.True(home.Service.IsPaused);
        Assert.True(home.HasGesture("Mine"));

        home.Service.SyncNow();
        home.WaitForRuns(2);
        SyncMachine.WaitFor(() => presenter.Asked.Count == 2, "the question again");
    }

    [Fact]
    public void ARunThatNeedsNoJoinAsksNothing()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        var presenter = new FakeSyncJoinPresenter();
        using var prompter = new SyncPrompter(home.Service, presenter, action => action());

        home.Service.Start();
        home.WaitForRuns(1);
        Thread.Sleep(100);

        Assert.Empty(presenter.Asked);
    }

    [Fact]
    public void TheDialogDefaultsToTheSyncedSettingsAndListsTheMachinesNewestFirst()
    {
        var older = new SyncMachineSummary(Guid.NewGuid(), "Mac", new DateTimeOffset(2026, 10, 6, 17, 5, 0, TimeSpan.Zero), 98, 8, 190);
        var newer = new SyncMachineSummary(Guid.NewGuid(), "PC-HOME", new DateTimeOffset(2026, 10, 7, 7, 40, 0, TimeSpan.Zero), 105, 9, 212);
        var vm = new SyncJoinViewModel([older, newer]);

        Assert.Equal(SyncJoin.UseRemote, vm.Choice);
        Assert.Equal(["PC-HOME", "Mac"], vm.Machines.Select(machine => machine.MachineName));
        Assert.Contains("replaced by PC-HOME's", vm.Explanation, StringComparison.Ordinal);
        Assert.Contains("backup", vm.Explanation, StringComparison.Ordinal);
        var written = newer.WrittenAt.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal($"Last written {written} · 105 gestures · 9 groups · 212 commands", SyncJoinViewModel.Describe(newer));

        var explanations = new List<string?>();
        vm.PropertyChanged += (_, e) => explanations.Add(e.PropertyName);
        vm.Choice = SyncJoin.Merge;
        Assert.Contains(nameof(SyncJoinViewModel.Explanation), explanations);
        Assert.Contains("appear twice", vm.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDialogFormHasTheMachinesAndTheTwoChoices()
    {
        var vm = new SyncJoinViewModel([new SyncMachineSummary(Guid.NewGuid(), "PC-WORK", DateTimeOffset.UnixEpoch, 3, 2, 4)]);

        var form = vm.Declare();

        Assert.Equal(["Already in the repository", "This machine"], form.Sections.Select(section => section.Title));
        Assert.Equal("PC-WORK", Assert.Single(form.Sections[0].Fields).Label);
        var choice = Assert.IsType<DropdownField<SyncJoin>>(form.Sections[1].Fields[0]);
        Assert.Equal([SyncJoinViewModel.UseRemoteLabel, SyncJoinViewModel.MergeLabel], choice.Choices.Select(c => c.Label));
        choice.Value.Set(SyncJoin.Merge);
        Assert.Equal(SyncJoin.Merge, vm.Choice);
    }
}
