using System.Globalization;
using Augram.App.Hosting;
using Augram.App.Tests.Sync.Support;
using Augram.App.Tray;
using Augram.App.ViewModels;
using Augram.Core.Config;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>Options › Sync: the fields over the settings store (drafts committed on leaving), the inline problems, the status texts, the conflicts line and Resolve….</summary>
public sealed class SyncViewModelTests : SyncTestBase
{
    private static readonly DateTimeOffset At = new(2026, 10, 7, 14, 32, 0, TimeSpan.Zero);
    private static readonly string Time = At.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    [Fact]
    public void TheFieldsShowTheStoredSyncSettings()
    {
        var home = Machine("PC-HOME", autoSync: false);
        using var vm = ViewModel(home);

        Assert.Equal(SyncMachine.Url, vm.RepositoryUrlText);
        Assert.Equal("PC-HOME", vm.MachineNameText);
        Assert.False(vm.AutoSync);
        Assert.True(vm.IsConfigured);
        Assert.True(vm.CanSyncNow);
        Assert.Null(vm.UrlProblem);
        Assert.Equal(SyncViewModel.NeverText, vm.StatusText);
    }

    [Fact]
    public void ATypedUrlIsCheckedAsTypedAndAppliedOnlyWhenCommittedAndValid()
    {
        var home = Machine("PC-HOME", url: null);
        using var vm = ViewModel(home);
        Assert.Equal(SyncViewModel.OffText, vm.StatusText);
        Assert.False(vm.CanSyncNow);

        vm.RepositoryUrlText = "https://joel:ghp_secret@github.com/joel/augram-settings.git";
        Assert.Equal(SyncSettingsRules.UserInfoProblem, vm.UrlProblem);
        Assert.Equal(SyncSettingsRules.UserInfoProblem, vm.UrlProblemText);
        vm.CommitRepositoryUrl();
        Assert.Null(home.Settings.Current.Sync.RepositoryUrl);

        vm.RepositoryUrlText = "  " + SyncMachine.Url;
        Assert.Null(vm.UrlProblem);
        Assert.Null(home.Settings.Current.Sync.RepositoryUrl);
        vm.CommitRepositoryUrl();
        Assert.Equal(SyncMachine.Url, home.Settings.Current.Sync.RepositoryUrl);
        Assert.Equal(SyncMachine.Url, vm.RepositoryUrlText);
        Assert.True(vm.IsConfigured);

        int changes = home.Settings.Version;
        vm.CommitRepositoryUrl();
        Assert.Equal(changes, home.Settings.Version);
    }

    [Fact]
    public void ABlankUrlTurnsSyncOffAndEscapeRevertsADraft()
    {
        var home = Machine("PC-HOME");
        using var vm = ViewModel(home);

        vm.RepositoryUrlText = "git@github";
        Assert.NotNull(vm.UrlProblem);
        vm.RevertRepositoryUrl();
        Assert.Equal(SyncMachine.Url, vm.RepositoryUrlText);
        Assert.Null(vm.UrlProblem);

        vm.RepositoryUrlText = "   ";
        Assert.Null(vm.UrlProblem);
        vm.CommitRepositoryUrl();
        Assert.Null(home.Settings.Current.Sync.RepositoryUrl);
        Assert.False(vm.IsConfigured);
    }

    [Fact]
    public void TheMachineNameCannotBeBlankAndIsAppliedOnCommit()
    {
        var home = Machine("PC-HOME");
        using var vm = ViewModel(home);

        vm.MachineNameText = " ";
        Assert.Equal(SyncViewModel.NameProblemText, vm.MachineNameProblem);
        vm.CommitMachineName();
        Assert.Equal("PC-HOME", home.Settings.Current.Sync.MachineName);

        vm.MachineNameText = "Laptop ";
        Assert.Null(vm.MachineNameProblem);
        vm.CommitMachineName();
        Assert.Equal("Laptop", home.Settings.Current.Sync.MachineName);

        vm.MachineNameText = "Typing";
        vm.RevertMachineName();
        Assert.Equal("Laptop", vm.MachineNameText);
    }

    [Fact]
    public void ADraftBeingTypedSurvivesAnUnrelatedSettingsChange()
    {
        var home = Machine("PC-HOME");
        using var vm = ViewModel(home);

        vm.RepositoryUrlText = "https://github.com/joel/half";
        home.Settings.SetGeneral(home.Settings.Current.General with { Enabled = false });

        Assert.Equal("https://github.com/joel/half", vm.RepositoryUrlText);
    }

    [Fact]
    public void AutomaticSyncIsOneStoreChangePerClick()
    {
        var home = Machine("PC-HOME");
        using var vm = ViewModel(home);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        int version = home.Settings.Version;

        vm.AutoSync = false;
        vm.AutoSync = false;

        Assert.False(home.Settings.Current.Sync.AutoSync);
        Assert.Equal(version + 1, home.Settings.Version);
        Assert.Contains(nameof(SyncViewModel.AutoSync), changed);
    }

    [Theory]
    [InlineData(false, false, false, null, SyncViewModel.OffText)]
    [InlineData(true, true, false, null, SyncViewModel.RunningText)]
    [InlineData(true, false, true, null, SyncViewModel.PausedText)]
    [InlineData(true, false, false, null, SyncViewModel.NeverText)]
    [InlineData(true, false, false, SyncStatus.Off, SyncViewModel.NeverText)]
    [InlineData(true, false, false, SyncStatus.NeedsJoinChoice, SyncViewModel.PausedText)]
    public void TheStatusLineSaysWhatIsGoingOn(bool configured, bool running, bool paused, SyncStatus? status, string expected)
    {
        var report = status is { } s ? new SyncReport(s, At) : null;

        Assert.Equal(expected, SyncViewModel.Status(configured, running, paused, report, []));
    }

    [Fact]
    public void ANewerAugramElsewhereReadsTheSameInOptionsTheTrayAndTheHealthLine()
    {
        var report = new SyncReport(SyncStatus.NeedsUpdate, At)
        {
            NewerMachines = [new SyncNewerMachine(Guid.NewGuid(), "Mac", IsThisMachine: false, SyncFile.CurrentFormatVersion + 1, ConfigDocument.CurrentSchemaVersion)],
        };

        Assert.Equal(
            "Paused: Mac uses a newer Augram. Update this machine (pull, rebuild, restart) to resume.",
            SyncViewModel.Status(true, false, true, report, ["Mac"]));
        Assert.Equal("sync paused: Mac uses a newer Augram", SyncTrayItem.Short(true, false, true, report));
        Assert.Equal("Paused (Mac uses a newer Augram)", SyncHealthContributor.Outcome(report));
        Assert.Equal("UpToDate", SyncHealthContributor.Outcome(new SyncReport(SyncStatus.UpToDate, At)));
    }

    [Fact]
    public void TheStatusLineForAFinishedRunHasItsTimeAndResult()
    {
        var applied = new SyncReport(SyncStatus.Applied, At)
        {
            Counts = SyncCounts.None with { Commands = new SyncKindCounts(1, 2, 0) },
        };
        var both = applied with { Counts = applied.Counts with { Gestures = new SyncKindCounts(0, 1, 0), Groups = new SyncKindCounts(2, 0, 0) } };

        Assert.Equal($"Up to date (last synced {Time}).", SyncViewModel.Status(true, false, false, new SyncReport(SyncStatus.UpToDate, At), ["Mac"]));
        Assert.Equal($"Last synced {Time}: 3 commands changed from Mac.", SyncViewModel.Status(true, false, false, applied, ["Mac"]));
        Assert.Equal($"Last synced {Time}: 1 gesture, 2 app groups and 3 commands changed from Mac and PC-WORK.", SyncViewModel.Status(true, false, false, both, ["Mac", "PC-WORK"]));
        Assert.Equal("1 hold remap and 3 commands changed", SyncViewModel.Changes(applied.Counts with { HoldRemaps = new SyncKindCounts(0, 1, 0) }));
        Assert.Equal(
            $"Sync failed at {Time}: sign-in needed: GitHub refused the credentials",
            SyncViewModel.Status(true, false, false, new SyncReport(SyncStatus.Failed, At) { Error = "sign-in needed: GitHub refused the credentials" }, ["Mac"]));
    }

    [Fact]
    public void TheStatusFollowsTheServiceAndTheConflictsLineAppearsOnlyWithConflicts()
    {
        var (work, home) = Joined();
        using var vm = ViewModel(home);
        var changed = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, e) => changed.Enqueue(e.PropertyName);
        Assert.False(vm.HasConflicts);
        Assert.Equal(string.Empty, vm.ConflictsText);

        home.Service.Start();
        home.WaitForRuns(1);
        SyncMachine.WaitFor(() => vm.StatusText.StartsWith("Up to date", StringComparison.Ordinal), "the up-to-date status");
        Assert.Contains(nameof(SyncViewModel.StatusText), changed);
        Assert.False(vm.HasConflicts);

        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        home.Gestures.Rename(home.Gesture("Up").Id, "South");
        work.SyncDirect();
        home.Service.SyncNow();
        home.WaitForRuns(2);

        Assert.True(vm.HasConflicts);
        Assert.Equal("1 conflict with PC-WORK", vm.ConflictsText);
        Assert.Equal("South", Assert.Single(vm.Conflicts).Name);
    }

    [Fact]
    public async Task ResolveHandsTheChoicesToTheServiceWhichResolvesThenSyncs()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        home.Gestures.Rename(home.Gesture("Up").Id, "South");
        work.SyncDirect();
        var presenter = new FakeSyncConflictPresenter { Choose = _ => SyncChoice.TakeTheirs };
        using var vm = ViewModel(home, presenter);
        home.Service.Start();
        home.WaitForRuns(1);
        Assert.True(vm.HasConflicts);

        await vm.ResolveConflictsAsync();
        home.WaitForRuns(2);

        Assert.Single(presenter.Shown);
        Assert.Equal(SyncTrigger.Resolve, home.Service.LastTrigger);
        Assert.True(home.HasGesture("North"));
        Assert.False(home.HasGesture("South"));
        Assert.False(vm.HasConflicts);
    }

    [Fact]
    public async Task CancellingTheConflictDialogLeavesEverythingPending()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        home.Gestures.Rename(home.Gesture("Up").Id, "South");
        work.SyncDirect();
        var presenter = new FakeSyncConflictPresenter { Choose = null };
        using var vm = ViewModel(home, presenter);
        home.Service.Start();
        home.WaitForRuns(1);

        await vm.ResolveConflictsAsync();
        Thread.Sleep(200);

        Assert.Equal(1, home.Service.RunCount);
        Assert.True(vm.HasConflicts);
        Assert.True(home.HasGesture("South"));
    }

    [Fact]
    public void TheDetailsListTheLastRunsRepairsAndNotes()
    {
        var (work, home) = Joined();
        using var vm = ViewModel(home);
        Assert.False(vm.HasDetails);
        work.Gestures.Add(SyncMachine.NewGesture("Zig", 50, 50));
        home.Gestures.Add(SyncMachine.NewGesture("Zig", -50, 50));
        work.SyncDirect();

        home.Service.Start();
        home.WaitForRuns(1);

        Assert.True(vm.HasDetails);
        Assert.Contains("Zig", vm.DetailsText, StringComparison.Ordinal);
    }

    private static SyncViewModel ViewModel(SyncMachine machine, FakeSyncConflictPresenter? presenter = null)
        => new(machine.Settings, machine.Service, presenter ?? new FakeSyncConflictPresenter(), action => action());
}
