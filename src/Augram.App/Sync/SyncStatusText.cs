using System.Globalization;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Every sync status in words (F8 sync), one place: <see cref="Status"/> is Options › Sync's line and
/// <see cref="Short"/> the tray tooltip's, both lookups over the <see cref="SyncPhase"/> of a
/// <see cref="SyncSnapshot"/>; <see cref="Outcome"/> is Diagnostics › Health's, over the last report alone.
/// </summary>
public static class SyncStatusText
{
    public const string OffText = "Off: paste a repository address above to sync gestures and commands with your other machines.";
    public const string RunningText = "Syncing…";
    public const string PausedText = "Paused: choose how to join (Sync now asks again).";
    public const string NeverText = "Not synced yet.";

    /// <summary>The Options › Sync status line: "Up to date (last synced 14:32).", "Last synced 14:32: 3 commands changed from Mac.", "Sync failed at 14:32: …".</summary>
    public static string Status(SyncSnapshot sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        return sync.Phase switch
        {
            SyncPhase.Off => OffText,
            SyncPhase.Running => RunningText,
            SyncPhase.PausedForUpdate => NeedsUpdateText(Last(sync).NewerMachines),
            SyncPhase.PausedForJoin => PausedText,
            SyncPhase.NotYet => NeverText,
            SyncPhase.UpToDate => $"Up to date (last synced {At(sync)}).",
            SyncPhase.Applied => $"Last synced {At(sync)}: {Changes(Last(sync).Counts)}{From(sync.OtherMachines)}.",
            _ => $"Sync failed at {At(sync)}: {Last(sync).Error ?? "unknown error"}",
        };
    }

    /// <summary>For the tray tooltip: "synced 14:32", "sync failed 14:32", "sync paused", "sync paused: Mac uses a newer Augram", "syncing…"; null while off or before the first run.</summary>
    public static string? Short(SyncSnapshot sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        return sync.Phase switch
        {
            SyncPhase.Off or SyncPhase.NotYet => null,
            SyncPhase.Running => "syncing…",
            SyncPhase.PausedForUpdate => $"sync paused: {SyncNewerMachine.Summary(Last(sync).NewerMachines)}",
            SyncPhase.PausedForJoin => "sync paused",
            SyncPhase.Failed => $"sync failed {At(sync)}",
            _ => $"synced {At(sync)}",
        };
    }

    /// <summary>For the health summary: the last report's status ("UpToDate"), or "Paused (Mac uses a newer Augram)" for <see cref="SyncStatus.NeedsUpdate"/>.</summary>
    public static string Outcome(SyncReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Status == SyncStatus.NeedsUpdate ? $"Paused ({SyncNewerMachine.Summary(report.NewerMachines)})" : report.Status.ToString();
    }

    /// <summary>"Paused: Mac uses a newer Augram. Update this machine (pull, rebuild, restart) to resume."</summary>
    public static string NeedsUpdateText(IReadOnlyList<SyncNewerMachine> machines)
        => $"Paused: {SyncNewerMachine.Summary(machines)}. Update this machine (pull, rebuild, restart) to resume.";

    /// <summary>"3 commands changed", "1 gesture, 2 groups and 3 commands changed".</summary>
    public static string Changes(SyncCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return counts.Phrase(kind => kind.Total) is { Length: > 0 } changed ? changed + " changed" : "nothing changed";
    }

    private static string From(IReadOnlyList<string> machines) => machines.Count == 0 ? string.Empty : " from " + string.Join(" and ", machines);

    private static string At(SyncSnapshot sync) => Last(sync).When.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    // Every phase after NotYet has a report.
    private static SyncReport Last(SyncSnapshot sync) => sync.LastReport ?? throw new InvalidOperationException($"Sync phase {sync.Phase} without a report.");
}
