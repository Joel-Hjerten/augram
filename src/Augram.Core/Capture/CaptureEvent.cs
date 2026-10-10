using Augram.Core.Abstractions;

namespace Augram.Core.Capture;

/// <summary>
/// One input event fed to the <see cref="CaptureStateMachine"/>, already translated by the
/// Engine from the hook library's types. A closed set: the nested records are the only cases.
/// Timestamps are milliseconds on one monotonic clock; the machine never reads a clock itself.
/// </summary>
public abstract record CaptureEvent(long TimestampMs)
{
    /// <summary>
    /// A physical button press. <paramref name="CaptureAllowed"/> is the Engine's verdict on the
    /// window under the pointer (ignored app, full-screen rule); <paramref name="IgnoreKeyHeld"/>
    /// is the modifier state re-read at this press, never tracked across events (reference §9).
    /// <paramref name="Modifiers"/> are the keys held at the press (a press's Before keys);
    /// <paramref name="Plan"/> is the anchor plan the hook read for the window under the pointer
    /// at this press, so the machine decides from what the hook decided from. <paramref name="Drags"/>
    /// is that window's drag distance per anchor (plan 0004), read with it; it never changes a
    /// decision of the hook's, only when the machine hands an anchor's press back.
    /// </summary>
    public sealed record ButtonDown(
        MouseButton Button,
        int X,
        int Y,
        long TimestampMs,
        bool CaptureAllowed = true,
        bool IgnoreKeyHeld = false,
        KeyModifiers Modifiers = KeyModifiers.None,
        AnchorPlan Plan = default,
        AnchorDragDistances Drags = default) : CaptureEvent(TimestampMs);

    /// <summary>A physical button release.</summary>
    public sealed record ButtonUp(MouseButton Button, int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>Pointer movement. The Engine need not forward moves while the machine is <see cref="CaptureState.Idle"/>.</summary>
    public sealed record Move(int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>One wheel tick.</summary>
    public sealed record Wheel(WheelDirection Direction, int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>A clock pulse from the Engine's timer; the only way the hold-still cancel can fire.</summary>
    public sealed record Tick(long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>
    /// A Ctrl, Alt, Shift or Win key went down while a press was held. <paramref name="Consumed"/> is the hook's decision
    /// (keys are paired per press by the Engine's key shadow, not by the machine): a consumed key the press had not seen is
    /// one of its After keys. The machine returns no input decision for it.
    /// </summary>
    public sealed record Key(KeyModifiers Modifier, bool Consumed, long TimestampMs) : CaptureEvent(TimestampMs);
}
