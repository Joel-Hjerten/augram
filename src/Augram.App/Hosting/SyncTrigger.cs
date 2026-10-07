namespace Augram.App.Hosting;

/// <summary>
/// Why <see cref="SyncService"/> ran (F8 sync, "when"). The automatic ones (<see cref="Start"/>,
/// <see cref="LocalChange"/>, <see cref="Poll"/>, <see cref="AutoSyncOn"/>) run only while a repository is set,
/// automatic sync is on and no join choice is pending; the others are the user's and always run.
/// </summary>
public enum SyncTrigger
{
    /// <summary>Once when the app starts.</summary>
    Start,

    /// <summary>20 s after the last gesture or command change the sync itself did not make (or a new machine name).</summary>
    LocalChange,

    /// <summary>Every 5 minutes while the app runs.</summary>
    Poll,

    /// <summary>Automatic sync was switched on.</summary>
    AutoSyncOn,

    /// <summary>Sync now (Options › Sync, the tray menu).</summary>
    Manual,

    /// <summary>The repository URL was set, changed or cleared.</summary>
    RepositoryChanged,

    /// <summary>The answer to the join question.</summary>
    Join,

    /// <summary>Conflict resolutions, followed by a run.</summary>
    Resolve,
}
