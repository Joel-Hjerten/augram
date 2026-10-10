using System.Runtime.InteropServices;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// The event-posting part of the macOS P/Invoke surface (hold remaps, plan 0002 step 3a, learnings 0005): Quartz Event
/// Services for building a mouse or keyboard event, retyping it, setting its flags and fields, and reading the session's
/// modifier flags. <c>CGEventCreate…</c> results are owned by the caller and released with <c>CFRelease</c>; posting is
/// <see cref="CGEventPost"/>. Constants are Apple's (<c>CGEventTypes.h</c>, <c>CGEventSource.h</c>); the event types and
/// flags themselves live with the pure rules in <c>Input/MacRemapButtonEvents</c>. C <c>bool</c> travels as a byte.
/// </summary>
internal static partial class MacNative
{
    /// <summary><c>kCGEventSourceStateCombinedSessionState</c>: every source's state together, what the user holds now.</summary>
    public const int CGEventSourceStateCombinedSession = 0;

    /// <summary><c>kCGMouseEventClickState</c>: the click count of a mouse down or up.</summary>
    public const uint CGMouseEventClickState = 1;

    /// <summary><c>kCGMouseEventDeltaX</c>: the relative motion of a move or drag.</summary>
    public const uint CGMouseEventDeltaX = 4;

    /// <summary><c>kCGMouseEventDeltaY</c>.</summary>
    public const uint CGMouseEventDeltaY = 5;

    /// <summary>A mouse event of <paramref name="mouseType"/> for <paramref name="mouseButton"/> at a global top-left point; a zero <paramref name="source"/> posts it as from no source.</summary>
    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, byte keyDown);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial void CGEventSetType(nint cgEvent, uint type);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial void CGEventSetFlags(nint cgEvent, ulong flags);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial void CGEventSetIntegerValueField(nint cgEvent, uint field, long value);

    /// <summary>The modifier flags of a source state (<see cref="CGEventSourceStateCombinedSession"/>: the keys held now).</summary>
    [LibraryImport(CoreGraphicsLibrary)]
    public static partial ulong CGEventSourceFlagsState(int stateId);
}
