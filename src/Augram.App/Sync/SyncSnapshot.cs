using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// What the status lines read, taken at one moment (<see cref="SyncService.Snapshot"/> takes it under the sync lock):
/// the <see cref="SyncPhase"/>, whether a repository is set and a run is going, the last report and the machines it
/// synced with ("from Mac").
/// </summary>
public sealed record SyncSnapshot(SyncPhase Phase, bool IsConfigured, bool IsRunning, SyncReport? LastReport, IReadOnlyList<string> OtherMachines)
{
    /// <summary>The snapshot for this state, its phase decided once: off, running, paused for an update, paused for the join question, not yet, then the last report's status.</summary>
    public static SyncSnapshot Of(bool configured, bool running, bool paused, SyncReport? report, IReadOnlyList<string> otherMachines)
    {
        ArgumentNullException.ThrowIfNull(otherMachines);
        return new SyncSnapshot(PhaseOf(configured, running, paused, report), configured, running, report, otherMachines);
    }

    private static SyncPhase PhaseOf(bool configured, bool running, bool paused, SyncReport? report)
    {
        if (!configured)
        {
            return SyncPhase.Off;
        }

        if (running)
        {
            return SyncPhase.Running;
        }

        if (report?.Status == SyncStatus.NeedsUpdate)
        {
            return SyncPhase.PausedForUpdate;
        }

        if (paused || report?.Status == SyncStatus.NeedsJoinChoice)
        {
            return SyncPhase.PausedForJoin;
        }

        if (report is null || report.Status == SyncStatus.Off)
        {
            return SyncPhase.NotYet;
        }

        return report.Status switch
        {
            SyncStatus.UpToDate => SyncPhase.UpToDate,
            SyncStatus.Applied => SyncPhase.Applied,
            _ => SyncPhase.Failed,
        };
    }
}
