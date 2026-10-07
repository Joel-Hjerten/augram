namespace Augram.Core.Sync;

/// <summary>
/// The result of <see cref="SyncCoordinator.Run"/> or <see cref="SyncCoordinator.Resolve"/>, for Options › Sync:
/// what happened, what changed here, the conflicts still waiting (all of them, not only new ones), the repairs
/// the merge made, notes (a skipped file, a "keep both" taken as "take theirs"), for
/// <see cref="SyncStatus.NeedsJoinChoice"/> the other machines to choose from, and for
/// <see cref="SyncStatus.NeedsUpdate"/> the machines a newer Augram wrote for.
/// </summary>
public sealed record SyncReport(SyncStatus Status, DateTimeOffset When)
{
    public SyncCounts Counts { get; init; } = SyncCounts.None;

    public IReadOnlyList<SyncConflict> Conflicts { get; init; } = [];

    public IReadOnlyList<SyncRepair> Repairs { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Filled for <see cref="SyncStatus.NeedsJoinChoice"/>: the machines whose files are in the repo.</summary>
    public IReadOnlyList<SyncMachineSummary> OtherMachines { get; init; } = [];

    /// <summary>Filled for <see cref="SyncStatus.NeedsUpdate"/>: whose file is newer than this build, this machine's own last publish included.</summary>
    public IReadOnlyList<SyncNewerMachine> NewerMachines { get; init; } = [];

    /// <summary>One line when <see cref="Status"/> is <see cref="SyncStatus.Failed"/>; never a credential or a URL.</summary>
    public string? Error { get; init; }
}
