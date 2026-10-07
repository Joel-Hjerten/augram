namespace Augram.Sync.Git;

/// <summary>
/// The local clone as git commands see it: runs git inside <see cref="Folder"/> with the local or
/// network timeout and answers the small questions the sync flows ask (is it a clone, does a ref
/// exist, is there an unpushed commit, which identity to commit with).
/// </summary>
internal sealed class GitClone
{
    public const string RemoteMain = "refs/remotes/origin/main";

    private readonly GitRunner _runner;
    private readonly string _fallbackName;

    public GitClone(string folder, GitRunner runner, string fallbackName)
    {
        Folder = folder;
        _runner = runner;
        _fallbackName = fallbackName;
    }

    public string Folder { get; }

    /// <summary>A <c>.git</c> folder (or worktree file) directly in <see cref="Folder"/>; a parent repository never counts.</summary>
    public bool Exists => Directory.Exists(Path.Combine(Folder, ".git")) || File.Exists(Path.Combine(Folder, ".git"));

    public bool IsAbsentOrEmpty => !Directory.Exists(Folder) || !Directory.EnumerateFileSystemEntries(Folder).Any();

    public GitResult Local(params string[] arguments) => _runner.Run(Folder, GitRunner.LocalTimeout, arguments);

    public GitResult Network(params string[] arguments) => _runner.Run(Folder, GitRunner.NetworkTimeout, arguments);

    /// <summary><c>git clone -- url folder</c> from the parent folder; <c>--</c> keeps a URL starting with '-' from being read as an option.</summary>
    public GitResult CloneFrom(string url)
    {
        var parent = Path.GetDirectoryName(Folder) ?? Folder;
        Directory.CreateDirectory(parent);
        return _runner.Run(parent, GitRunner.NetworkTimeout, ["clone", "--", url, Folder]);
    }

    public bool HasRef(string name) => Local("rev-parse", "--verify", "--quiet", name).Succeeded;

    /// <summary>True when HEAD has commits the remote's <c>main</c> lacks (or the remote has no <c>main</c> yet).</summary>
    public bool HasUnpushedCommits()
    {
        if (!HasRef("HEAD"))
        {
            return false;
        }

        if (!HasRef(RemoteMain))
        {
            return true;
        }

        var ahead = Local("rev-list", "--count", RemoteMain + "..HEAD");
        return ahead.Succeeded && ahead.Output.Trim() != "0";
    }

    /// <summary>
    /// <paramref name="arguments"/> prefixed with the fallback identity when the user has no
    /// <c>user.name</c> and <c>user.email</c> configured. Used for every command that writes a commit
    /// (commit, rebase, and the autostash a rebase may make).
    /// </summary>
    public string[] WithIdentity(params string[] arguments) =>
        IsConfigured("user.name") && IsConfigured("user.email")
            ? arguments
            : [.. CommitIdentity.FallbackArguments(_fallbackName), .. arguments];

    private bool IsConfigured(string key)
    {
        var value = Local("config", "--get", key);
        return value.Succeeded && value.Output.Trim().Length > 0;
    }
}
