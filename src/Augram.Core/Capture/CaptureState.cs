namespace Augram.Core.Capture;

/// <summary>
/// Where the <see cref="CaptureStateMachine"/> is between an anchor press and its release.
/// Every state except <see cref="Idle"/> means "we consumed a button-down and owe the OS a consumed button-up".
/// </summary>
public enum CaptureState
{
    /// <summary>Nothing held. Every event passes through.</summary>
    Idle,

    /// <summary>Anchor down, pointer has not yet moved <see cref="CaptureThresholds.StartDistancePx"/>. Release here is a click.</summary>
    Held,

    /// <summary>A stroke is being recorded (stroke button only). Release here completes it.</summary>
    Drawing,

    /// <summary>A wheel tick fired while held. Movement is ignored, every further tick fires again, release fires nothing.</summary>
    WheelFiring,

    /// <summary>The gesture was cancelled (other button, or held still). Waiting for the release so it can be consumed.</summary>
    Cancelled,

    /// <summary>
    /// A held-back anchor that is not the stroke button was given back to the app (it moved past the start distance, waited
    /// past the hold-still time, or another button went down): its down was injected at the start point. Waiting for the
    /// physical release, which is consumed and injected in its place, so the app's down and up stay a pair.
    /// </summary>
    HandedBack,

    /// <summary>
    /// A button trigger fired (plan 0005): a button the plan fires for this anchor went down while it was held back. Frozen like
    /// <see cref="WheelFiring"/>: no hand-back, moves and wheel ticks pass; the fired button's release ends what it holds, and a
    /// new press of it fires again; the anchor's release is consumed and replays nothing.
    /// </summary>
    ButtonFiring,
}
