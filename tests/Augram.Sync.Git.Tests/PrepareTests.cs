using Xunit;

namespace Augram.Sync.Git.Tests;

public sealed class PrepareTests : IDisposable
{
    private readonly GitSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void PrepareOnAnEmptyRemoteMakesACloneWhoseFirstCommitLandsOnMain()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.Machine("machine-a");

        GitSandbox.AssertOk(machine.Prepare(remote));

        Assert.True(Directory.Exists(Path.Combine(machine.Folder, ".git")));
        Assert.True(Directory.Exists(Path.Combine(machine.Folder, "machines")));
        Assert.Equal("refs/heads/main", _sandbox.Git(machine.Folder, "symbolic-ref", "HEAD"));
        Assert.Empty(machine.ReadMachineFiles());
    }

    [Fact]
    public void PrepareAgainWithTheSameRepositorySpelledDifferentlyIsFine()
    {
        var remote = _sandbox.CreateRemote();
        var machine = _sandbox.PreparedMachine("machine-a", remote);

        GitSandbox.AssertOk(machine.Prepare(remote));
        GitSandbox.AssertOk(machine.Prepare(remote + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void PrepareRefusesAFolderThatIsACloneOfAnotherRepository()
    {
        var first = _sandbox.CreateRemote("first.git");
        var second = _sandbox.CreateRemote("second.git");
        var machine = _sandbox.PreparedMachine("machine-a", first);

        var result = machine.Prepare(second);

        Assert.False(result.Succeeded);
        Assert.Contains("is a clone of", result.Error);
    }

    [Fact]
    public void PrepareRefusesANonEmptyFolderThatIsNotAClone()
    {
        var remote = _sandbox.CreateRemote();
        var folder = _sandbox.FolderFor("machine-a");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "mine");

        var result = _sandbox.Machine("machine-a").Prepare(remote);

        Assert.False(result.Succeeded);
        Assert.Contains("not empty and is not a git clone", result.Error);
        Assert.False(Directory.Exists(Path.Combine(folder, ".git")));
    }

    [Fact]
    public void PrepareIntoAnExistingEmptyFolderClones()
    {
        var remote = _sandbox.CreateRemote();
        Directory.CreateDirectory(_sandbox.FolderFor("machine-a"));

        GitSandbox.AssertOk(_sandbox.Machine("machine-a").Prepare(remote));
    }

    [Fact]
    public void AMissingGitExecutableGivesTheNotInstalledLine()
    {
        var remote = _sandbox.CreateRemote();
        var machine = new GitSyncRepository(_sandbox.FolderFor("machine-a"), Path.Combine(_sandbox.Root, "no-such-git"), "test-pc");

        var result = machine.Prepare(remote);

        Assert.False(result.Succeeded);
        Assert.Equal(GitFailure.NotInstalled, result.Error);
    }

    [Fact]
    public void ANonexistentRemoteGivesTheNotFoundLineAndNoException()
    {
        var machine = _sandbox.Machine("machine-a");

        var result = machine.Prepare(Path.Combine(_sandbox.Root, "missing.git"));

        Assert.False(result.Succeeded);
        Assert.Equal(GitFailure.NotFound, result.Error);
        Assert.False(Directory.Exists(Path.Combine(machine.Folder, ".git")));
    }

    [Theory]
    [InlineData("https://joel:ghp_secret123@example.invalid/joel/augram-settings.git")]
    [InlineData("https://ghp_secret123@example.invalid/joel/augram-settings.git")]
    public void PrepareRefusesAUrlCarryingACredentialAndNeverRepeatsIt(string url)
    {
        var machine = _sandbox.Machine("machine-a");

        var result = machine.Prepare(url);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("ghp_secret123", result.Error);
        Assert.Contains("https://example.invalid/joel/augram-settings.git", result.Error);
        Assert.False(Directory.Exists(machine.Folder));
    }

    [Fact]
    public void PullAndPublishBeforePrepareFailWithoutThrowing()
    {
        var machine = _sandbox.Machine("machine-a");

        Assert.False(machine.Pull().Succeeded);
        Assert.False(machine.Publish("machine-a", "{}", "first").Succeeded);
        Assert.Empty(machine.ReadMachineFiles());
    }
}
