using Augram.Core.Capture;

namespace Augram.Core.Abstractions;

/// <summary>
/// One physical input event as an <see cref="IInputSource"/> delivers it: already translated
/// to Core's enums, stamped with <see cref="IClock.MonotonicMs"/>, simulated input filtered out (except another program's
/// button release, delivered as <see cref="RawInputKind.ButtonReleasedElsewhere"/>, and wheel ticks that are not Augram's own).
/// A struct, so delivering one allocates nothing on the hook thread. Which fields apply depends
/// on <see cref="Kind"/> (see <see cref="RawInputKind"/>); the others are default.
/// </summary>
public readonly record struct RawInput(
    RawInputKind Kind,
    long TimestampMs,
    int X = 0,
    int Y = 0,
    MouseButton Button = MouseButton.Left,
    WheelDirection Wheel = WheelDirection.Up,
    KeyCode Key = KeyCode.None,
    KeyModifiers Modifiers = KeyModifiers.None)
{
    public static RawInput ButtonDown(MouseButton button, int x, int y, long timestampMs, KeyModifiers modifiers = KeyModifiers.None)
        => new(RawInputKind.ButtonDown, timestampMs, x, y, button, Modifiers: modifiers);

    public static RawInput ButtonUp(MouseButton button, int x, int y, long timestampMs, KeyModifiers modifiers = KeyModifiers.None)
        => new(RawInputKind.ButtonUp, timestampMs, x, y, button, Modifiers: modifiers);

    public static RawInput ButtonReleasedElsewhere(MouseButton button, int x, int y, long timestampMs)
        => new(RawInputKind.ButtonReleasedElsewhere, timestampMs, x, y, button);

    public static RawInput Move(int x, int y, long timestampMs) => new(RawInputKind.Move, timestampMs, x, y);

    public static RawInput WheelTick(WheelDirection direction, int x, int y, long timestampMs, KeyModifiers modifiers = KeyModifiers.None)
        => new(RawInputKind.Wheel, timestampMs, x, y, Wheel: direction, Modifiers: modifiers);

    public static RawInput KeyDown(KeyCode key, long timestampMs, KeyModifiers modifiers = KeyModifiers.None)
        => new(RawInputKind.KeyDown, timestampMs, Key: key, Modifiers: modifiers);

    public static RawInput KeyUp(KeyCode key, long timestampMs, KeyModifiers modifiers = KeyModifiers.None)
        => new(RawInputKind.KeyUp, timestampMs, Key: key, Modifiers: modifiers);
}
