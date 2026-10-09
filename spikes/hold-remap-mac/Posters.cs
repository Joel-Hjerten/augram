using SharpHook;
using SharpHook.Data;

namespace HoldRemapSpike;

internal enum Modifier
{
    None,
    Shift,
    Control,
}

/// <summary>How the worker puts events into the system (<c>--post sharphook</c> or <c>--post native</c>). Worker thread only.</summary>
internal interface IPoster
{
    string Describe { get; }

    bool ModifierDown(Modifier modifier);

    bool ModifierUp(Modifier modifier);

    /// <summary>Middle down at the input event's position; <paramref name="heldModifier"/> is the modifier pressed around it.</summary>
    bool MiddleDown(int x, int y, Modifier heldModifier);

    /// <summary>Middle up wherever the pointer is now.</summary>
    bool MiddleUp();

    bool SpaceTap();
}

/// <summary>What Augram posts with today (<c>Engine/Input/SharpHookInputSimulator</c>).</summary>
internal sealed class SharpHookPoster : IPoster
{
    private readonly EventSimulator _simulator = new();

    public string Describe => "sharphook: SharpHook's EventSimulator, as Augram posts today";

    public bool ModifierDown(Modifier modifier) => Ok(_simulator.SimulateKeyPress(Key(modifier)));

    public bool ModifierUp(Modifier modifier) => Ok(_simulator.SimulateKeyRelease(Key(modifier)));

    // libuiohook numbers buttons 1 left, 2 right, 3 middle (Augram's MouseButtonMap).
    public bool MiddleDown(int x, int y, Modifier heldModifier) =>
        Ok(_simulator.SimulateMousePress(Clamp(x), Clamp(y), MouseButton.Button3));

    public bool MiddleUp() => Ok(_simulator.SimulateMouseRelease(MouseButton.Button3));

    public bool SpaceTap() => Ok(_simulator.SimulateKeyPress(KeyCode.VcSpace)) & Ok(_simulator.SimulateKeyRelease(KeyCode.VcSpace));

    private static KeyCode Key(Modifier modifier) => modifier == Modifier.Shift ? KeyCode.VcLeftShift : KeyCode.VcLeftControl;

    private static short Clamp(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static bool Ok(UioHookResult result) => result == UioHookResult.Success;
}

/// <summary>
/// CoreGraphics directly, posted at the HID tap with no event source: a modifier as a flagsChanged event (a keyboard
/// event retyped, with the flags it leaves set), Middle as an other-mouse event of the centre button carrying the
/// modifier's flags, Space as key code 49. Posted events come back through the hook as simulated and are dropped there.
/// </summary>
internal sealed class NativePoster : IPoster
{
    public string Describe => "native: CoreGraphics CGEventPost at the HID tap";

    public bool ModifierDown(Modifier modifier) => PostFlagsChanged(KeyOf(modifier), keyDown: true, Native.NonCoalescedFlag | FlagsOf(modifier));

    public bool ModifierUp(Modifier modifier) => PostFlagsChanged(KeyOf(modifier), keyDown: false, Native.NonCoalescedFlag);

    public bool MiddleDown(int x, int y, Modifier heldModifier) =>
        PostMouse(Native.OtherMouseDown, new Native.CGPoint { X = x, Y = y }, heldModifier == Modifier.None ? null : FlagsOf(heldModifier));

    public bool MiddleUp() => PostMouse(Native.OtherMouseUp, Native.PointerLocation(), flags: null);

    public bool SpaceTap() => PostKey(Native.SpaceKey, keyDown: true) & PostKey(Native.SpaceKey, keyDown: false);

    /// <summary>
    /// Variant B: one swallowed physical drag re-posted as a centre-button drag at the same position, with the delta
    /// from the previous physical position and the session's modifier flags as they are now. Used with either backend.
    /// </summary>
    public static bool Dragged(int x, int y, int dx, int dy)
    {
        var cgEvent = Native.CGEventCreateMouseEvent(0, Native.OtherMouseDragged, new Native.CGPoint { X = x, Y = y }, Native.CenterButton);
        if (cgEvent == 0)
        {
            return false;
        }

        Native.CGEventSetIntegerValueField(cgEvent, Native.DeltaXField, dx);
        Native.CGEventSetIntegerValueField(cgEvent, Native.DeltaYField, dy);
        Native.CGEventSetFlags(cgEvent, Native.CGEventSourceFlagsState(Native.CombinedSessionState));
        return Post(cgEvent);
    }

    private static ushort KeyOf(Modifier modifier) => modifier == Modifier.Shift ? Native.ShiftKey : Native.ControlKey;

    private static ulong FlagsOf(Modifier modifier) => modifier switch
    {
        Modifier.Shift => Native.ShiftFlag | Native.LeftShiftDeviceFlag,
        Modifier.Control => Native.ControlFlag | Native.LeftControlDeviceFlag,
        _ => 0,
    };

    private static bool PostFlagsChanged(ushort key, bool keyDown, ulong flags)
    {
        var cgEvent = Native.CGEventCreateKeyboardEvent(0, key, keyDown ? (byte)1 : (byte)0);
        if (cgEvent == 0)
        {
            return false;
        }

        Native.CGEventSetType(cgEvent, Native.FlagsChanged);
        Native.CGEventSetFlags(cgEvent, flags);
        return Post(cgEvent);
    }

    private static bool PostMouse(uint type, Native.CGPoint at, ulong? flags)
    {
        var cgEvent = Native.CGEventCreateMouseEvent(0, type, at, Native.CenterButton);
        if (cgEvent == 0)
        {
            return false;
        }

        Native.CGEventSetIntegerValueField(cgEvent, Native.ClickStateField, 1);
        if (flags is { } mask)
        {
            Native.CGEventSetFlags(cgEvent, mask);
        }

        return Post(cgEvent);
    }

    private static bool PostKey(ushort key, bool keyDown)
    {
        var cgEvent = Native.CGEventCreateKeyboardEvent(0, key, keyDown ? (byte)1 : (byte)0);
        return cgEvent != 0 && Post(cgEvent);
    }

    private static bool Post(nint cgEvent)
    {
        Native.CGEventPost(Native.HidEventTap, cgEvent);
        Native.CFRelease(cgEvent);
        return true;
    }
}
