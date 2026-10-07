using Augram.Core.Abstractions;

namespace Augram.Sync.Git;

/// <summary>
/// <see cref="ISyncRepository"/> over the installed git (requirements F8 sync): a local clone of a
/// private repo with one <c>machines/&lt;machine-id&gt;.json</c> per machine on branch <c>main</c>.
/// Each machine commits only its own file, so pulls rebase without conflicts and pushes never need
/// force; a rejected push is pulled and retried once. Authentication is git's credential helper;
/// this class never sees a credential and refuses a URL that carries one. Expected failures come
/// back as <see cref="SyncOperationResult.Failed"/> lines (see <see cref="GitFailure"/>), never as
/// exceptions. Called from the sync worker only; every call may block for seconds.
/// </summary>
public sealed class GitSyncRepository : ISyncRepository
{
    private const string PushTarget = "HEAD:refs/heads/main";

    private readonly GitClone _clone;

    /// <param name="folder">The clone's folder (created by <see cref="Prepare"/>), e.g. <c>&lt;config&gt;/sync/repo</c>.</param>
    /// <param name="gitExecutable"><c>git</c> from PATH, or a full path to the executable.</param>
    /// <param name="machineLabel">Shown in the fallback commit identity, "Augram (label)"; defaults to the computer name.</param>
    public GitSyncRepository(string folder, string gitExecutable = "git", string? machineLabel = null)
        : this(folder, new GitRunner(gitExecutable), machineLabel)
    {
    }

    internal GitSyncRepository(string folder, GitRunner runner, string? machineLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(runner);
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        _clone = new GitClone(fullPath, runner, CommitIdentity.FallbackName(machineLabel ?? Environment.MachineName));
    }

    public string Folder => _clone.Folder;

    public SyncOperationResult Prepare(string repositoryUrl) => Guarded(() => PrepareClone(repositoryUrl));

    public SyncOperationResult Pull() => Guarded(PullFromRemote);

    public IReadOnlyDictionary<string, string> ReadMachineFiles() => MachineFiles.ReadAll(Folder);

    public SyncOperationResult Publish(string machineId, string content, string message) =>
        Guarded(() => PublishFile(machineId, content, message));

    private SyncOperationResult PrepareClone(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return Failed("no repository URL is set");
        }

        var url = repositoryUrl.Trim();
        if (RemoteUrl.CarriesCredentials(url))
        {
            return Failed($"the repository URL {url} contains a user name or token; use the plain URL and let git's credential helper sign in");
        }

        var ready = _clone.Exists ? CheckOrigin(url) : CloneInto(url);
        if (!ready.Succeeded)
        {
            return ready;
        }

        // HEAD is unborn after cloning an empty remote, or one whose default branch is not main (a bare
        // repo's "master" while only main exists): put HEAD on main and take origin/main if there is one.
        if (!_clone.HasRef("HEAD"))
        {
            var head = _clone.Local("symbolic-ref", "HEAD", "refs/heads/main");
            if (!head.Succeeded)
            {
                return Failed(head, "symbolic-ref");
            }

            var take = TakeRemoteMainIntoUnbornHead();
            if (!take.Succeeded)
            {
                return take;
            }
        }

        Directory.CreateDirectory(Path.Combine(Folder, MachineFiles.FolderName));
        return SyncOperationResult.Ok;
    }

    private SyncOperationResult TakeRemoteMainIntoUnbornHead()
    {
        if (!_clone.HasRef(GitClone.RemoteMain))
        {
            return SyncOperationResult.Ok;
        }

        var merge = _clone.Local("merge", "--ff-only", GitClone.RemoteMain);
        return merge.Succeeded ? SyncOperationResult.Ok : Failed(merge, "merge");
    }

    private SyncOperationResult CloneInto(string url)
    {
        if (!_clone.IsAbsentOrEmpty)
        {
            return Failed($"the sync folder {Folder} is not empty and is not a git clone; choose an empty folder");
        }

        var clone = _clone.CloneFrom(url);
        return clone.Succeeded ? SyncOperationResult.Ok : Failed(clone, "clone");
    }

    private SyncOperationResult CheckOrigin(string url)
    {
        var origin = _clone.Local("config", "--get", "remote.origin.url");
        if (origin.Status != GitRunStatus.Completed)
        {
            return Failed(origin, "config");
        }

        var current = origin.Output.Trim();
        if (!origin.Succeeded || current.Length == 0)
        {
            return Failed($"the sync folder {Folder} is a git clone without an origin; choose another folder");
        }

        return RemoteUrl.SameRepository(current, url)
            ? SyncOperationResult.Ok
            : Failed($"the sync folder {Folder} is a clone of {current}, not {url}; choose another folder or remove this one");
    }

    // fetch, then rebase onto origin/main: this machine's unpushed commit is replayed on top. Only
    // this machine's own file differs, so the rebase cannot conflict unless someone edited the
    // clone by hand; then it is aborted and reported.
    private SyncOperationResult PullFromRemote()
    {
        if (!_clone.Exists)
        {
            return NotPrepared();
        }

        var fetch = _clone.Network("fetch", "--prune", "origin");
        if (!fetch.Succeeded)
        {
            return Failed(fetch, "fetch");
        }

        if (!_clone.HasRef(GitClone.RemoteMain))
        {
            return SyncOperationResult.Ok; // the remote has no branch yet
        }

        if (!_clone.HasRef("HEAD"))
        {
            return TakeRemoteMainIntoUnbornHead();
        }

        var rebase = _clone.Local(_clone.WithIdentity("rebase", "--autostash", GitClone.RemoteMain));
        if (rebase.Succeeded)
        {
            return SyncOperationResult.Ok;
        }

        _clone.Local("rebase", "--abort");
        return SyncOperationResult.Failed(GitFailure.Describe(rebase, "rebase", Folder) + " (the pull was undone)");
    }

    private SyncOperationResult PublishFile(string machineId, string content, string message)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!MachineFiles.IsValidId(machineId))
        {
            return Failed($"'{machineId}' is not a usable machine id: letters, digits and '-' only, at most {MachineFiles.MaxIdLength}");
        }

        if (!_clone.Exists)
        {
            return NotPrepared();
        }

        var path = MachineFiles.RelativePath(machineId);
        MachineFiles.Write(Folder, machineId, content);
        var add = _clone.Local("add", "--", path);
        if (!add.Succeeded)
        {
            return Failed(add, "add");
        }

        // Exit 1 means the staged file differs from HEAD; 0 means nothing to commit.
        var diff = _clone.Local("diff", "--cached", "--quiet", "--", path);
        if (diff.Status != GitRunStatus.Completed || diff.ExitCode > 1)
        {
            return Failed(diff, "diff");
        }

        if (diff.ExitCode == 1)
        {
            var text = string.IsNullOrWhiteSpace(message) ? $"Update {path}" : message;
            var commit = _clone.Local(_clone.WithIdentity("commit", "-m", text, "--", path));
            if (!commit.Succeeded)
            {
                return Failed(commit, "commit");
            }
        }
        else if (!_clone.HasUnpushedCommits())
        {
            return SyncOperationResult.Ok;
        }

        return Push();
    }

    // Never --force: a rejection means another machine pushed first; take its commits and try once more.
    private SyncOperationResult Push()
    {
        var push = _clone.Network("push", "origin", PushTarget);
        if (push.Succeeded)
        {
            return SyncOperationResult.Ok;
        }

        if (!IsRejectedAsBehind(push))
        {
            return Failed(push, "push");
        }

        var pull = PullFromRemote();
        if (!pull.Succeeded)
        {
            return pull;
        }

        var retry = _clone.Network("push", "origin", PushTarget);
        return retry.Succeeded ? SyncOperationResult.Ok : Failed(retry, "push");
    }

    private static bool IsRejectedAsBehind(GitResult push) =>
        push.Status == GitRunStatus.Completed
        && (push.Error.Contains("[rejected]", StringComparison.Ordinal)
            || push.Error.Contains("(fetch first)", StringComparison.Ordinal)
            || push.Error.Contains("(non-fast-forward)", StringComparison.Ordinal));

    private SyncOperationResult NotPrepared() =>
        Failed($"the sync folder {Folder} is not a git clone yet; prepare it first");

    private SyncOperationResult Failed(GitResult result, string operation) =>
        SyncOperationResult.Failed(GitFailure.Describe(result, operation, Folder));

    private static SyncOperationResult Failed(string line) => SyncOperationResult.Failed(CredentialScrubber.Scrub(line));

    // The folder itself can fail (no permission, disk full, a file locked by another program).
    private static SyncOperationResult Guarded(Func<SyncOperationResult> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed($"the sync folder could not be used: {exception.Message}");
        }
    }
}
