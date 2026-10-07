namespace Augram.Core.Diagnostics;

/// <summary>
/// The health summary (N4): one immutable value per read, every field null until the component that
/// owns it has contributed. Built by <see cref="HealthRegistry"/> from registered contributors.
/// <see cref="LastSyncOutcome"/> and <see cref="LastSyncAt"/> are the last machine-to-machine sync (F8):
/// its status ("UpToDate", "Applied", "Failed", …) and when it finished.
/// </summary>
public sealed record HealthSnapshot(
    DateTimeOffset? HookAliveSince = null,
    int? HookReinstallCount = null,
    int? EventsLastMinute = null,
    double? LastStrokeLatencyMs = null,
    string? LastActivationOutcome = null,
    double? OverlayFirstFrameMs = null,
    double? UptimeSeconds = null,
    long? WorkingSetBytes = null,
    string? LastSyncOutcome = null,
    DateTimeOffset? LastSyncAt = null)
{
    public static HealthSnapshot Empty { get; } = new();
}
