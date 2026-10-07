using Augram.Core.Abstractions;
using Xunit;

namespace Augram.Sync.Git.Tests;

/// <summary>
/// One temp root per test: bare "remotes" made with <c>git init --bare</c> and clones acting as
/// machines, all local paths (no network). Git runs cut off from the developer's own config:
/// <c>GIT_CONFIG_GLOBAL</c> points at a file in the root (empty unless the test writes an identity
/// into it) and <c>GIT_CONFIG_NOSYSTEM=1</c>; identity variables are removed. No real config is
/// ever read or written. The root is deleted on dispose, read-only git objects included.
/// </summary>
internal sealed class GitSandbox : IDisposable
{
    public GitSandbox(string? globalConfig = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "augram-sync-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Root);
        GlobalConfig = Path.Combine(Root, "global.gitconfig");
        File.WriteAllText(GlobalConfig, globalConfig ?? string.Empty);
        Runner = new GitRunner("git", new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = GlobalConfig,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_AUTHOR_NAME"] = null,
            ["GIT_AUTHOR_EMAIL"] = null,
            ["GIT_COMMITTER_NAME"] = null,
            ["GIT_COMMITTER_EMAIL"] = null,
            ["EMAIL"] = null,
        });
    }

    public string Root { get; }

    public string GlobalConfig { get; }

    public GitRunner Runner { get; }

    /// <summary>
    /// An empty bare repository under the root; its path is the "URL". Its HEAD names
    /// <paramref name="defaultBranch"/>: "master" (git's classic default, a branch Augram never pushes)
    /// unless a test asks for "main", as a repo created on GitHub has.
    /// </summary>
    public string CreateRemote(string name = "remote.git", string defaultBranch = "master")
    {
        var path = Path.Combine(Root, name);
        Git(Root, "init", "--bare", "--quiet", "--initial-branch=" + defaultBranch, path);
        return path;
    }

    public string FolderFor(string machine) => Path.Combine(Root, machine);

    public GitSyncRepository Machine(string machine, string? label = null) => new(FolderFor(machine), Runner, label ?? machine);

    /// <summary>A machine already prepared on <paramref name="remote"/>.</summary>
    public GitSyncRepository PreparedMachine(string machine, string remote)
    {
        var repository = Machine(machine);
        AssertOk(repository.Prepare(remote));
        return repository;
    }

    /// <summary>Runs git for an assertion and returns its trimmed output; fails the test when git fails.</summary>
    public string Git(string workingDirectory, params string[] arguments)
    {
        var result = Runner.Run(workingDirectory, GitRunner.LocalTimeout, arguments);
        Assert.True(result.Succeeded, $"git {string.Join(' ', arguments)} failed: {result.Error}");
        return result.Output.Trim();
    }

    public static void AssertOk(SyncOperationResult result) =>
        Assert.True(result.Succeeded, $"expected success, got: {result.Error}");

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temp folder left behind is harmless; a failing Dispose would hide the test's own result.
        }
    }
}
