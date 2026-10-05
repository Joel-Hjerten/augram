namespace Augram.Core.Capture;

/// <summary>
/// Where the <see cref="CaptureStateMachine"/> is between a stroke-button press and its release.
/// Every state except <see cref="Idle"/> means "we consumed a button-down and owe the OS a consumed button-up".
/// </summary>
public enum CaptureState
{
    /// <summary>Nothing held. Every event passes through.</summary>
    Idle,

    /// <summary>Stroke button down, pointer has not yet moved <see cref="CaptureThresholds.StartDistancePx"/>. Release here is a click.</summary>
    Held,

    /// <summary>A stroke is being recorded. Release here completes it.</summary>
    Drawing,

    /// <summary>A wheel tick fired while held. Movement is ignored, every further tick fires again, release fires nothing.</summary>
    WheelFiring,

    /// <summary>The gesture was cancelled (other button, or held still). Waiting for the release so it can be consumed.</summary>
    Cancelled,
}
