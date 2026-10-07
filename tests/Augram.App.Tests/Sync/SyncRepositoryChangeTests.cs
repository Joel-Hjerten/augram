using Augram.App.Hosting;
using Augram.App.Tests.Sync.Support;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>
/// A new repository URL (or none) starts over (F8 sync): the old repository's state is cleared so the join question
/// comes again, and the old clone is moved aside to <c>sync/repo-old-…</c>, never deleted, because the git adapter
/// refuses a clone of another URL.
/// </summary>
public sealed class SyncRepositoryChangeTests : SyncTestBase
{
    [Fact]
    public void ANewUrlClearsTheStateMovesTheCloneAsideAndAsksTheJoinQuestionAgain()
    {
        var (_, home) = Joined();
        home.Service.Start();
        home.WaitForRuns(1);
        Assert.False(home.Bases.IsEmpty);
        var head = FakeClone(home);

        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = SyncMachine.OtherUrl });
        home.WaitForRuns(2);

        Assert.Equal(SyncTrigger.RepositoryChanged, home.Service.LastTrigger);
        Assert.Equal(SyncStatus.NeedsJoinChoice, home.Service.LastReport!.Status);
        Assert.True(home.Service.IsPaused);
        Assert.True(home.Bases.IsEmpty);
        Assert.Equal(SyncMachine.OtherUrl, home.Repository.PreparedUrls[^1]);
        Assert.Equal(SyncMachine.OtherUrl, home.Folders.RememberedUrl);
        Assert.False(Directory.Exists(home.Folders.RepositoryFolder));
        var old = Assert.Single(Directory.GetDirectories(home.Folders.Root, SyncFolders.OldRepositoryPrefix + "*"));
        Assert.Equal(head, File.ReadAllText(Path.Combine(old, ".git", "HEAD")));
        Assert.True(home.Log.Has(SyncService.LogSource, "Sync state cleared: the repository changed"));
    }

    [Fact]
    public void ANewUrlRunsEvenWithAutomaticSyncOff()
    {
        var home = Machine("PC-HOME", url: null, autoSync: false);
        home.Service.Start();

        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = SyncMachine.Url });
        home.WaitForRuns(1);

        Assert.Equal(SyncTrigger.RepositoryChanged, home.Service.LastTrigger);
        Assert.Equal(SyncStatus.UpToDate, home.Service.LastReport!.Status);
        Assert.Equal(SyncMachine.Url, home.Folders.RememberedUrl);
    }

    [Fact]
    public void ClearingTheUrlTurnsSyncOffAndForgetsTheState()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Up").Id, "North");
        home.Gestures.Rename(home.Gesture("Up").Id, "South");
        work.SyncDirect();
        home.Service.Start();
        home.WaitForRuns(1);
        Assert.NotEmpty(home.Service.PendingConflicts());
        FakeClone(home);

        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = null });
        home.WaitForRuns(2);

        Assert.Equal(SyncStatus.Off, home.Service.LastReport!.Status);
        Assert.False(home.Service.IsConfigured);
        Assert.Empty(home.Service.PendingConflicts());
        Assert.True(home.Bases.IsEmpty);
        Assert.Null(home.Folders.RememberedUrl);
        Assert.Single(Directory.GetDirectories(home.Folders.Root, SyncFolders.OldRepositoryPrefix + "*"));
        Assert.True(home.HasGesture("South"), "turning sync off never touches the gestures");

        home.Service.SyncNow();
        Thread.Sleep(200);
        Assert.Equal(2, home.Service.RunCount);
    }

    [Fact]
    public void SettingTheSameUrlAgainAfterClearingAsksTheJoinQuestionAgain()
    {
        var (_, home) = Joined();
        home.Service.Start();
        home.WaitForRuns(1);

        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = null });
        home.WaitForRuns(2);
        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = SyncMachine.Url });
        home.WaitForRuns(3);

        Assert.Equal(SyncStatus.NeedsJoinChoice, home.Service.LastReport!.Status);
    }

    /// <summary>Puts a stand-in clone in the repo folder (the fake repository writes nothing there) and returns its HEAD text.</summary>
    private static string FakeClone(SyncMachine machine)
    {
        var git = Path.Combine(machine.Folders.RepositoryFolder, ".git");
        Directory.CreateDirectory(git);
        const string head = "ref: refs/heads/main";
        File.WriteAllText(Path.Combine(git, "HEAD"), head);
        return head;
    }
}
