namespace Augram.Sync.Git;

/// <summary>How one git process ended, before its exit code is looked at.</summary>
internal enum GitRunStatus
{
    /// <summary>The process ran and exited; see <see cref="GitResult.ExitCode"/>.</summary>
    Completed,

    /// <summary>The git executable could not be started (missing, not on PATH, not executable).</summary>
    NotInstalled,

    /// <summary>The process outlived its timeout and its process tree was killed.</summary>
    TimedOut,
}
