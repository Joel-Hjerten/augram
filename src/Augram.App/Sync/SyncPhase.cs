using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Where sync stands, as the status lines say it (F8 sync): computed once by <see cref="SyncSnapshot.Of"/>, in this
/// order, and rendered by <see cref="SyncStatusText"/> for Options › Sync and the tray tooltip.
/// </summary>
public enum SyncPhase
{
    /// <summary>No repository is set.</summary>
    Off,

    /// <summary>A run is going.</summary>
    Running,

    /// <summary>The last run answered <see cref="SyncStatus.NeedsUpdate"/>: a newer Augram wrote to the repo.</summary>
    PausedForUpdate,

    /// <summary>Automatic runs are paused, or the last run answered <see cref="SyncStatus.NeedsJoinChoice"/>: the join question waits.</summary>
    PausedForJoin,

    /// <summary>No run yet, or the last one found no repository (<see cref="SyncStatus.Off"/>).</summary>
    NotYet,

    /// <summary>The last run answered <see cref="SyncStatus.UpToDate"/>.</summary>
    UpToDate,

    /// <summary>The last run answered <see cref="SyncStatus.Applied"/>.</summary>
    Applied,

    /// <summary>The last run answered <see cref="SyncStatus.Failed"/>.</summary>
    Failed,
}
