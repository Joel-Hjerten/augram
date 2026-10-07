namespace Augram.Sync.Git;

/// <summary>
/// What one git process left behind: how it ended, its exit code and its captured output.
/// Output may contain a credentialed URL; anything shown or returned goes through
/// <see cref="CredentialScrubber"/> first (<see cref="GitFailure"/> does).
/// </summary>
internal sealed record GitResult(GitRunStatus Status, int ExitCode, string Output, string Error)
{
    public static GitResult NotInstalled { get; } = new(GitRunStatus.NotInstalled, -1, string.Empty, string.Empty);

    /// <summary>Ran to the end and exited 0.</summary>
    public bool Succeeded => Status == GitRunStatus.Completed && ExitCode == 0;
}
