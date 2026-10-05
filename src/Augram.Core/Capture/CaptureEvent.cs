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
    /// </summary>
    public sealed record ButtonDown(
        MouseButton Button,
        int X,
        int Y,
        long TimestampMs,
        bool CaptureAllowed = true,
        bool IgnoreKeyHeld = false) : CaptureEvent(TimestampMs);

    /// <summary>A physical button release.</summary>
    public sealed record ButtonUp(MouseButton Button, int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>Pointer movement. The Engine need not forward moves while the machine is <see cref="CaptureState.Idle"/>.</summary>
    public sealed record Move(int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>One wheel tick.</summary>
    public sealed record Wheel(WheelDirection Direction, int X, int Y, long TimestampMs) : CaptureEvent(TimestampMs);

    /// <summary>A clock pulse from the Engine's timer; the only way the hold-still cancel can fire.</summary>
    public sealed record Tick(long TimestampMs) : CaptureEvent(TimestampMs);
}
