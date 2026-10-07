using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.App.Tests.Sync.Support;
using Augram.App.ViewModels;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>
/// The sync worker (F8 sync, "when"): the start-up run, Sync now, coalescing, the 20 s change delay that ignores the
/// sync's own changes, the 5 minute poll (only with automatic sync on), the pause on the join question and on a newer
/// Augram in the repo, and stopping.
/// Timers are a <see cref="ManualSchedule"/>; the repository is in memory.
/// </summary>
public sealed class SyncServiceTests : SyncTestBase
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void RunsOnceAtStartAndPublishesThisMachinesFile()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Up")]);

        home.Service.Start();
        home.WaitForRuns(1);

        Assert.Equal(SyncTrigger.Start, home.Service.LastTrigger);
        Assert.Equal(SyncStatus.UpToDate, home.Service.LastReport!.Status);
        Assert.Equal([SyncMachine.Url], home.Repository.PreparedUrls);
        Assert.True(Remote.Files.ContainsKey(home.Settings.Current.Sync.MachineId.ToString("D")));
        Assert.False(home.Service.IsRunning);
    }

    [Fact]
    public void NothingRunsWithoutARepository()
    {
        var home = Machine("PC-HOME", url: null);

        home.Service.Start();
        home.Service.SyncNow();
        home.Gestures.Add(SyncMachine.NewGesture("Up"));
        Thread.Sleep(Quiet);

        Assert.Equal(0, home.Service.RunCount);
        Assert.Empty(home.Repository.PreparedUrls);
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));
        Assert.False(home.Service.IsConfigured);
    }

    [Fact]
    public void WithAutomaticSyncOffOnlySyncNowRuns()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Up")], autoSync: false);

        home.Service.Start();
        home.Gestures.Rename(home.Gesture("Up").Id, "North");
        Thread.Sleep(Quiet);
        Assert.Equal(0, home.Service.RunCount);
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));

        home.Service.SyncNow();
        home.WaitForRuns(1);

        Assert.Equal(SyncTrigger.Manual, home.Service.LastTrigger);
        Assert.Equal(0, home.Schedule.Pending(SyncService.PollInterval));
    }

    [Fact]
    public void RequestsDuringARunCoalesceIntoOneMoreRunAndRunsNeverOverlap()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Up")]);
        home.Repository.HoldPull();
        home.Service.Start();
        SyncMachine.WaitFor(() => home.Repository.Pulls == 1 && home.Service.IsRunning, "the start-up run to reach its pull");

        home.Service.SyncNow();
        home.Service.SyncNow();
        home.Service.SyncNow();
        home.Gestures.Rename(home.Gesture("Up").Id, "North");
        home.Repository.Release();
        home.WaitForRuns(2);
        Thread.Sleep(Quiet);

        Assert.Equal(2, home.Service.RunCount);
        Assert.Equal(2, home.Repository.Pulls);
        Assert.Equal(1, home.Repository.MaxConcurrent);
        Assert.Equal(SyncTrigger.Manual, home.Service.LastTrigger);
        Assert.Equal(0, home.Schedule.Pending(SyncService.ChangeDelay));
        Assert.Contains("North", Remote.Files[home.Settings.Current.Sync.MachineId.ToString("D")], StringComparison.Ordinal);
    }

    [Fact]
    public void ALocalChangeSyncsTwentySecondsAfterTheLastOne()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Up")]);
        home.Service.Start();
        home.WaitForRuns(1);

        home.Gestures.Rename(home.Gesture("Up").Id, "North");
        home.Mapping.AddCommand(GroupId.Global, MappingFixture.Unbound("Close"));

        Assert.Equal(2, home.Schedule.Scheduled(SyncService.ChangeDelay));
        Assert.Equal(1, home.Schedule.Pending(SyncService.ChangeDelay));
        Assert.Equal(1, home.Service.RunCount);
        Assert.Equal(1, home.Schedule.Fire(SyncService.ChangeDelay));
        home.WaitForRuns(2);
        Assert.Equal(SyncTrigger.LocalChange, home.Service.LastTrigger);
        var file = Remote.Files[home.Settings.Current.Sync.MachineId.ToString("D")];
        Assert.Contains("North", file, StringComparison.Ordinal);
        Assert.Contains("Close", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewMachineNameIsALocalChange()
    {
        var home = Machine("PC-HOME");
        home.Service.Start();
        home.WaitForRuns(1);

        home.Settings.SetSync(home.Settings.Current.Sync with { MachineName = "Laptop" });

        Assert.Equal(1, home.Schedule.Pending(SyncService.ChangeDelay));
    }

    [Fact]
    public void ChangesTheSyncAppliesAreNotLocalChanges()
    {
        var (work, home) = Joined();
        home.Service.Start();
        home.WaitForRuns(1);
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        work.SyncDirect();

        home.Service.SyncNow();
        home.WaitForRuns(2);

        Assert.Equal(SyncStatus.Applied, home.Service.LastReport!.Status);
        Assert.True(home.HasGesture("North"));
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));
        Assert.Equal(["PC-WORK"], home.Service.OtherMachines);
    }

    [Fact]
    public void PollsEveryFiveMinutesOnlyWhileAutomaticSyncIsOn()
    {
        var home = Machine("PC-HOME");
        home.Service.Start();
        home.WaitForRuns(1);
        Assert.Equal(1, home.Schedule.Pending(SyncService.PollInterval));

        home.Schedule.Fire(SyncService.PollInterval);
        home.WaitForRuns(2);
        Assert.Equal(SyncTrigger.Poll, home.Service.LastTrigger);
        Assert.Equal(1, home.Schedule.Pending(SyncService.PollInterval));

        home.Settings.SetSync(home.Settings.Current.Sync with { AutoSync = false });
        Assert.Equal(0, home.Schedule.Pending(SyncService.PollInterval));
        home.Service.SyncNow();
        home.WaitForRuns(3);
        Assert.Equal(0, home.Schedule.Pending(SyncService.PollInterval));

        home.Settings.SetSync(home.Settings.Current.Sync with { AutoSync = true });
        home.WaitForRuns(4);
        Assert.Equal(SyncTrigger.AutoSyncOn, home.Service.LastTrigger);
        Assert.Equal(1, home.Schedule.Pending(SyncService.PollInterval));
    }

    [Fact]
    public void TheJoinQuestionPausesAutomaticSyncUntilTheUserChooses()
    {
        Work();
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        home.Service.Start();
        home.WaitForRuns(1);

        var report = home.Service.LastReport!;
        Assert.Equal(SyncStatus.NeedsJoinChoice, report.Status);
        Assert.Equal("PC-WORK", Assert.Single(report.OtherMachines).MachineName);
        Assert.True(home.Service.IsPaused);
        Assert.Equal(0, home.Schedule.Pending(SyncService.PollInterval));
        home.Gestures.Rename(home.Gesture("Mine").Id, "Mine too");
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));

        home.Service.SyncNow();
        home.WaitForRuns(2);
        Assert.Equal(SyncStatus.NeedsJoinChoice, home.Service.LastReport!.Status);
        Assert.True(home.HasGesture("Mine too"), "nothing changes until the user chooses");

        home.Service.Join(SyncJoin.UseRemote);
        home.WaitForRuns(3);
        Assert.Equal(SyncTrigger.Join, home.Service.LastTrigger);
        Assert.Equal(SyncStatus.Applied, home.Service.LastReport!.Status);
        Assert.False(home.Service.IsPaused);
        Assert.True(home.HasGesture("Up"));
        Assert.False(home.HasGesture("Mine too"));
        Assert.Equal(1, home.Schedule.Pending(SyncService.PollInterval));
    }

    [Fact]
    public void ANewerAugramInTheRepoPausesAutomaticSyncAndSyncNowOnlyChecksAgain()
    {
        var (work, home) = Joined();
        var workKey = work.Settings.Current.Sync.MachineId.ToString("D");
        Remote.Write(workKey, Remote.Files[workKey].Replace(
            $"\"formatVersion\": {SyncFile.CurrentFormatVersion}",
            $"\"formatVersion\": {SyncFile.CurrentFormatVersion + 1}",
            StringComparison.Ordinal));
        var homeKey = home.Settings.Current.Sync.MachineId.ToString("D");
        var own = Remote.Files[homeKey];

        home.Service.Start();
        home.WaitForRuns(1);

        Assert.Equal(SyncStatus.NeedsUpdate, home.Service.LastReport!.Status);
        Assert.True(home.Service.IsPaused);
        Assert.Equal(0, home.Schedule.Pending(SyncService.PollInterval));
        home.Gestures.Rename(home.Gesture("Up").Id, "North");
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));
        Assert.Equal(
            "Paused: PC-WORK uses a newer Augram. Update this machine (pull, rebuild, restart) to resume.",
            SyncViewModel.Status(true, false, home.Service.IsPaused, home.Service.LastReport, home.Service.OtherMachines));

        home.Service.SyncNow();
        home.WaitForRuns(2);
        Assert.Equal(SyncStatus.NeedsUpdate, home.Service.LastReport!.Status);
        Assert.True(home.Service.IsPaused);
        Assert.Equal(own, Remote.Files[homeKey]);
        Assert.True(home.HasGesture("North"), "nothing here is merged over");
    }

    [Fact]
    public void AFailedRunIsReportedAndTheNextOneRecovers()
    {
        var home = Machine("PC-HOME");
        home.Repository.FailWith = "offline or the host cannot be reached";
        home.Service.Start();
        home.WaitForRuns(1);
        Assert.Equal(SyncStatus.Failed, home.Service.LastReport!.Status);
        Assert.Equal("offline or the host cannot be reached", home.Service.LastReport.Error);
        Assert.Equal(1, home.Schedule.Pending(SyncService.PollInterval));

        home.Repository.FailWith = null;
        home.Schedule.Fire(SyncService.PollInterval);
        home.WaitForRuns(2);
        Assert.Equal(SyncStatus.UpToDate, home.Service.LastReport!.Status);
    }

    [Fact]
    public void NothingRunsAfterDispose()
    {
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Up")]);
        home.Service.Start();
        home.WaitForRuns(1);

        home.Service.Dispose();
        home.Service.SyncNow();
        home.Gestures.Rename(home.Gesture("Up").Id, "North");
        home.Schedule.Fire(SyncService.PollInterval);
        Thread.Sleep(Quiet);

        Assert.Equal(1, home.Service.RunCount);
        Assert.Equal(0, home.Schedule.Scheduled(SyncService.ChangeDelay));
        home.Service.Dispose();
    }

    [Fact]
    public void ARunInProgressAtDisposeAppliesNothing()
    {
        var (work, home) = Joined();
        home.Service.Start();
        home.WaitForRuns(1);
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        work.SyncDirect();
        int pulls = home.Repository.Pulls;
        home.Repository.HoldPull();
        home.Service.SyncNow();
        SyncMachine.WaitFor(() => home.Repository.Pulls == pulls + 1 && home.Service.IsRunning, "the second run to reach its pull");

        // Dispose on its own thread (it waits for the worker), release the pull once the store thread has stopped.
        var disposing = new Thread(home.Service.Dispose);
        disposing.Start();
        SyncMachine.WaitFor(() => home.StoreThread.Run(() => SyncApplied.Done).Error == SyncStoreThread.ClosingError, "the store thread to stop");
        home.Repository.Release();
        Assert.True(disposing.Join(SyncMachine.Timeout));
        SyncMachine.WaitFor(() => home.Service.RunCount == 2, "the abandoned run to end");

        Assert.True(home.HasGesture("Up"));
        Assert.False(home.HasGesture("North"));
        Assert.Equal(SyncStatus.Failed, home.Service.LastReport!.Status);
        Assert.Equal(SyncStoreThread.ClosingError, home.Service.LastReport.Error);
    }
}
