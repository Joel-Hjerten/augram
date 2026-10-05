namespace Augram.Core.Capture;

/// <summary>
/// User-tunable capture numbers (Options page, F1). Defaults are Joel's StrokesPlus.net values.
/// </summary>
/// <param name="StartDistancePx">Displacement from the press point at which a hold becomes a stroke. Less is a click and is replayed.</param>
/// <param name="MinSegmentPx">A move is recorded only if at least this far from the last recorded point (decimation; classic StrokesPlus 6 px).</param>
/// <param name="CancelDelayMs">Holding still this long cancels the gesture (checklist A12/A13).</param>
/// <param name="ResetCancelDelayOnMovement">Whether each recorded point restarts the cancel delay. False makes it a hard deadline from the press.</param>
public sealed record CaptureThresholds(
    int StartDistancePx = 30,
    int MinSegmentPx = 6,
    int CancelDelayMs = 1000,
    bool ResetCancelDelayOnMovement = true)
{
    public static CaptureThresholds Default { get; } = new();
}
