namespace Augram.Core.Sync;

/// <summary>The headline of a <see cref="SyncReport"/>.</summary>
public enum SyncStatus
{
    /// <summary>No repository is set.</summary>
    Off,

    /// <summary>The sync ran and nothing here changed (this machine's file may still have been published).</summary>
    UpToDate,

    /// <summary>The sync changed gestures or commands here.</summary>
    Applied,

    /// <summary>First sync against a repo holding other machines' files: nothing changed; ask the user and run again with a <see cref="SyncJoin"/>.</summary>
    NeedsJoinChoice,

    /// <summary>The repository or a step failed; <see cref="SyncReport.Error"/> says why.</summary>
    Failed,
}
