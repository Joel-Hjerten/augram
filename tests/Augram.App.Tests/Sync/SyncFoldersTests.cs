using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary><see cref="SyncFolders.Prepare"/>: which repository the clone and the state belong to, kept across restarts in <c>sync/repository-url.txt</c>.</summary>
public sealed class SyncFoldersTests : IDisposable
{
    private const string First = "https://github.com/joel/augram-settings.git";
    private const string Second = "https://github.com/joel/other.git";

    private readonly string _config = Path.Combine(Path.GetTempPath(), "augram-sync-folder-tests", Guid.NewGuid().ToString("N"));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 7, 14, 32, 5, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_config))
        {
            Directory.Delete(_config, recursive: true);
        }
    }

    [Fact]
    public void TheFirstUrlIsRememberedAndWhatIsThereIsKept()
    {
        var folders = new SyncFolders(_config, _clock);
        var bases = State();
        Clone(folders);

        var result = folders.Prepare(First, bases);

        Assert.Equal(SyncFolderReset.None, result);
        Assert.Equal(First, folders.RememberedUrl);
        Assert.False(bases.IsEmpty);
        Assert.True(Directory.Exists(folders.RepositoryFolder));
        Assert.Equal(Path.Combine(_config, "sync", "repo"), folders.RepositoryFolder);
    }

    [Fact]
    public void TheSameUrlChangesNothing()
    {
        var folders = new SyncFolders(_config, _clock);
        var bases = State();
        folders.Prepare(First, bases);
        Clone(folders);

        Assert.Equal(SyncFolderReset.None, folders.Prepare(" " + First + " ", bases));
        Assert.False(bases.IsEmpty);
        Assert.True(Directory.Exists(folders.RepositoryFolder));
    }

    [Fact]
    public void AUrlChangedWhileTheAppWasClosedIsResetOnTheNextRun()
    {
        new SyncFolders(_config, _clock).Prepare(First, State());
        var folders = new SyncFolders(_config, _clock);
        Clone(folders);
        var bases = new SyncBaseStore(_config);

        var result = folders.Prepare(Second, bases);

        Assert.True(result.Reset);
        Assert.Equal(Path.Combine(folders.Root, "repo-old-20261007-143205"), result.MovedTo);
        Assert.True(File.Exists(Path.Combine(result.MovedTo!, ".git", "HEAD")));
        Assert.False(Directory.Exists(folders.RepositoryFolder));
        Assert.True(bases.IsEmpty);
        Assert.Equal(Second, folders.RememberedUrl);
    }

    [Fact]
    public void TwoMovesInTheSameSecondKeepBothOldClones()
    {
        var folders = new SyncFolders(_config, _clock);
        var bases = State();
        folders.Prepare(First, bases);
        Clone(folders);
        folders.Prepare(Second, bases);
        Clone(folders);

        var result = folders.Prepare(First, bases);

        Assert.Equal(Path.Combine(folders.Root, "repo-old-20261007-143205-2"), result.MovedTo);
        Assert.Equal(2, Directory.GetDirectories(folders.Root, SyncFolders.OldRepositoryPrefix + "*").Length);
    }

    [Fact]
    public void NoUrlForgetsTheStateAndTheRememberedUrl()
    {
        var folders = new SyncFolders(_config, _clock);
        var bases = State();
        folders.Prepare(First, bases);

        var result = folders.Prepare(null, bases);

        Assert.True(result.Reset);
        Assert.Null(result.MovedTo);
        Assert.True(bases.IsEmpty);
        Assert.Null(folders.RememberedUrl);
        Assert.Equal(SyncFolderReset.None, folders.Prepare(null, bases));
    }

    [Fact]
    public void AnEmptyCloneFolderIsLeftWhereItIs()
    {
        var folders = new SyncFolders(_config, _clock);
        folders.Prepare(First, State());
        Directory.CreateDirectory(folders.RepositoryFolder);

        var result = folders.Prepare(Second, new SyncBaseStore(_config));

        Assert.True(result.Reset);
        Assert.Null(result.MovedTo);
        Assert.True(Directory.Exists(folders.RepositoryFolder));
    }

    private SyncBaseStore State()
    {
        var bases = new SyncBaseStore(_config);
        bases.Save(new SyncMachineState(Guid.NewGuid(), "PC-WORK"));
        return bases;
    }

    private static void Clone(SyncFolders folders)
    {
        var git = Path.Combine(folders.RepositoryFolder, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main");
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now)
        {
            UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; }

        public long MonotonicMs => 0;
    }
}
