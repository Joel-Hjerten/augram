namespace Augram.Core.Capture;

/// <summary>
/// Mouse buttons as Core knows them. The Engine maps SharpHook's numbering (which is
/// libuiohook's, not Windows') onto this enum at the hook boundary; nothing in Core
/// sees a vendor or OS button code.
/// </summary>
public enum MouseButton
{
    Left,
    Middle,
    Right,
    X1,
    X2,
}
