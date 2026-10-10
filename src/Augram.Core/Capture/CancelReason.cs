namespace Augram.Core.Capture;

/// <summary>Why a gesture in progress was cancelled (F1, checklist A12/A13). A cancelled gesture fires nothing and replays nothing.</summary>
public enum CancelReason
{
    /// <summary>Another mouse button went down while the stroke button was held.</summary>
    OtherButton,

    /// <summary>The pointer stayed still past <see cref="CaptureThresholds.CancelDelayMs"/>.</summary>
    HoldStill,

    /// <summary>Another program posted the owner's release (plan 0005 decision 10): the press ends without a click or a command.</summary>
    ReleasedElsewhere,
}
