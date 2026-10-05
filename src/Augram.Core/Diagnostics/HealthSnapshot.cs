namespace Augram.Core.Diagnostics;

/// <summary>
/// The health summary (N4): one immutable value per read, every field null until the component that
/// owns it has contributed. Built by <see cref="HealthRegistry"/> from registered contributors.
/// </summary>
public sealed record HealthSnapshot(
    DateTimeOffset? HookAliveSince = null,
    int? HookReinstallCount = null,
    int? EventsLastMinute = null,
    double? LastStrokeLatencyMs = null,
    string? LastActivationOutcome = null,
    double? OverlayFirstFrameMs = null,
    double? UptimeSeconds = null,
    long? WorkingSetBytes = null)
{
    public static HealthSnapshot Empty { get; } = new();
}
