namespace Augram.Core.Capture;

/// <summary>
/// User-tunable capture numbers (Options page, F1). Defaults are Joel's StrokesPlus.net values.
/// </summary>
/// <param name="StartDistancePx">Displacement from the press point at which a hold becomes a stroke. Less is a click and is replayed.</param>
/// <param name="MinSegmentPx">A move is recorded only if at least this far from the last recorded point (decimation; classic StrokesPlus 6 px).</param>
/// <param name="CancelDelayMs">Holding still this long cancels the gesture (checklist A12/A13).</param>
/// <param name="ResetCancelDelayOnMovement">Whether each recorded point restarts the cancel delay. False makes it a hard deadline from the press.</param>
/// <param name="ButtonDragDistancePx">Displacement from the press point at which a held-back button other than the stroke button (an anchor, Right in
/// Right + wheel) is handed back to the app as a drag. It never draws, so it needs far less than <paramref name="StartDistancePx"/>; lower starts a
/// drag sooner (Joel, 2026-10-10: Spine and Eyeris pan with Right), too low gives the press away on a wobble while the wheel turns.</param>
public sealed record CaptureThresholds(
    int StartDistancePx = 30,
    int MinSegmentPx = 6,
    int CancelDelayMs = 1000,
    bool ResetCancelDelayOnMovement = true,
    int ButtonDragDistancePx = 10)
{
    /// <summary>The largest button drag distance, here and on a command (<c>Mapping.TriggerHold.DragDistancePx</c>); <see cref="AnchorDragDistances"/> packs it in 8 bits.</summary>
    public const int MaxButtonDragDistancePx = 200;

    public static CaptureThresholds Default { get; } = new();
}
