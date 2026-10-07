using Xunit;

namespace Augram.Sync.Git.Tests;

/// <summary>git's stderr (English, as <see cref="GitRunner"/> forces) to the one line the user sees.</summary>
public sealed class GitFailureTests
{
    private const string Folder = "C:/Users/joel/AppData/Roaming/Augram/sync/repo";

    [Theory]
    [InlineData("fatal: could not read Username for 'https://github.com': terminal prompts disabled")]
    [InlineData("remote: Invalid username or token. Password authentication is not supported for Git operations.\nfatal: Authentication failed for 'https://github.com/joel/augram-settings.git/'")]
    [InlineData("fatal: unable to access 'https://github.com/joel/augram-settings.git/': The requested URL returned error: 403")]
    [InlineData("git@github.com: Permission denied (publickey).\nfatal: Could not read from remote repository.")]
    public void RefusedCredentialsAskForASignIn(string error)
    {
        Assert.Equal(GitFailure.SignInNeeded(Folder), Describe(error));
        Assert.Contains(Folder, GitFailure.SignInNeeded(Folder));
    }

    [Theory]
    [InlineData("remote: Repository not found.\nfatal: repository 'https://github.com/joel/augram-settings.git/' not found")]
    [InlineData("fatal: repository 'C:/temp/missing.git' does not exist")]
    [InlineData("fatal: 'C:/temp/missing.git' does not appear to be a git repository\nfatal: Could not read from remote repository.")]
    public void AMissingRepositoryIsNotFound(string error)
    {
        Assert.Equal(GitFailure.NotFound, Describe(error));
    }

    [Theory]
    [InlineData("fatal: unable to access 'https://github.com/joel/augram-settings.git/': Could not resolve host: github.com")]
    [InlineData("fatal: unable to access 'https://github.com/joel/augram-settings.git/': Failed to connect to github.com port 443 after 21 ms: Couldn't connect to server")]
    [InlineData("ssh: connect to host github.com port 22: Network is unreachable\nfatal: Could not read from remote repository.")]
    public void NoNetworkIsOffline(string error)
    {
        Assert.Equal(GitFailure.Offline, Describe(error));
    }

    [Fact]
    public void AnythingElseIsGitsLastRealLineWithoutPrefixOrHints()
    {
        var error = " ! [remote rejected] HEAD -> main (protected branch hook declined)\n"
            + "error: failed to push some refs to 'https://github.com/joel/augram-settings.git'\n"
            + "hint: Updates were rejected because of a hook.\n";

        Assert.Equal("git push failed: failed to push some refs to 'https://github.com/joel/augram-settings.git'", Describe(error));
    }

    [Fact]
    public void NoOutputAtAllGivesTheExitCode()
    {
        Assert.Equal("git push failed with exit code 128", Describe(string.Empty));
    }

    [Fact]
    public void AMissingExecutableAndATimeoutHaveTheirOwnLines()
    {
        Assert.Equal(GitFailure.NotInstalled, GitFailure.Describe(GitResult.NotInstalled, "push", Folder));
        var late = new GitResult(GitRunStatus.TimedOut, -1, string.Empty, string.Empty);
        Assert.Equal("git push did not finish in time and was stopped; try again later", GitFailure.Describe(late, "push", Folder));
    }

    [Fact]
    public void EveryLineIsScrubbedOfCredentials()
    {
        var error = "fatal: unable to access 'https://joel:ghp_secret123@github.com/joel/augram-settings.git/': SSL certificate problem: unable to get local issuer certificate";

        var line = Describe(error);

        Assert.DoesNotContain("ghp_secret123", line);
        Assert.DoesNotContain("joel:", line);
        Assert.Contains("'https://github.com/joel/augram-settings.git/'", line);
    }

    private static string Describe(string error) =>
        GitFailure.Describe(new GitResult(GitRunStatus.Completed, 128, string.Empty, error), "push", Folder);
}
