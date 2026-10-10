using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The pure half of <see cref="MacRemapButtonSimulator"/>, tested on every OS (plan 0002 step 3a, learnings 0005): which
/// CoreGraphics event types and button number a hold remap's button output is posted as, and what its modifiers become. A
/// modifier is a <c>kCGEventFlagsChanged</c> event of its virtual key whose flags are what is held once it has changed: the
/// device-independent mask, the left-key device bit, and <c>NX_NONCOALSESCEDMASK</c> (0x100, set on every real event). They
/// go down in Ctrl, Option, Shift, Command order (the order the Windows simulator presses them) and up in reverse; the
/// button's down carries every mask and device bit, without 0x100. Constants are Apple's (<c>CGEventTypes.h</c>,
/// <c>Events.h</c>, <c>IOLLEvent.h</c>).
/// </summary>
internal static class MacRemapButtonEvents
{
    /// <summary><c>kCGEventFlagsChanged</c>.</summary>
    public const uint FlagsChanged = 12;

    /// <summary><c>NX_NONCOALSESCEDMASK</c>.</summary>
    public const ulong NonCoalesced = 0x100;

    // kCGEvent{Left,Right,Other}Mouse{Down,Up,Dragged}.
    private const uint LeftMouseDown = 1;
    private const uint LeftMouseUp = 2;
    private const uint RightMouseDown = 3;
    private const uint RightMouseUp = 4;
    private const uint LeftMouseDragged = 6;
    private const uint RightMouseDragged = 7;
    private const uint OtherMouseDown = 25;
    private const uint OtherMouseUp = 26;
    private const uint OtherMouseDragged = 27;

    // The device-independent masks (kCGEventFlagMask…) and the left-key device bits (NX_DEVICEL…KEYMASK).
    private const ulong ControlFlags = 0x0004_0000 | 0x01;
    private const ulong AlternateFlags = 0x0008_0000 | 0x20;
    private const ulong ShiftFlags = 0x0002_0000 | 0x02;
    private const ulong CommandFlags = 0x0010_0000 | 0x08;

    // kVK_Control, kVK_Option, kVK_Shift, kVK_Command (the left keys).
    private static readonly (KeyModifiers Modifier, ushort VirtualKey, ulong Flags)[] ModifierKeys =
    [
        (KeyModifiers.Control, 59, ControlFlags),
        (KeyModifiers.Alt, 58, AlternateFlags),
        (KeyModifiers.Shift, 56, ShiftFlags),
        (KeyModifiers.Meta, 55, CommandFlags),
    ];

    /// <summary>The down, up and dragged event types for <paramref name="button"/> and its <c>CGMouseButton</c> number (0 left, 1 right, 2 centre, then the side buttons).</summary>
    public static MacMouseEvents For(MouseButton button) => button switch
    {
        MouseButton.Left => new(LeftMouseDown, LeftMouseUp, LeftMouseDragged, 0),
        MouseButton.Right => new(RightMouseDown, RightMouseUp, RightMouseDragged, 1),
        MouseButton.Middle => new(OtherMouseDown, OtherMouseUp, OtherMouseDragged, 2),
        MouseButton.X1 => new(OtherMouseDown, OtherMouseUp, OtherMouseDragged, 3),
        MouseButton.X2 => new(OtherMouseDown, OtherMouseUp, OtherMouseDragged, 4),
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Not a mouse button."),
    };

    /// <summary>The flagsChanged events around a button's down for <paramref name="modifiers"/> (only the four count), and the flags the down carries.</summary>
    public static MacModifierEvents Modifiers(KeyModifiers modifiers)
    {
        var downs = new List<MacFlagsChange>(ModifierKeys.Length);
        var held = 0UL;
        foreach (var (modifier, virtualKey, flags) in ModifierKeys)
        {
            if ((modifiers & modifier) != 0)
            {
                held |= flags;
                downs.Add(new MacFlagsChange(virtualKey, held | NonCoalesced));
            }
        }

        var ups = new List<MacFlagsChange>(downs.Count);
        for (var i = downs.Count - 1; i >= 0; i--)
        {
            // Once this key is up, what the downs before it left held.
            var left = i == 0 ? 0 : downs[i - 1].Flags & ~NonCoalesced;
            ups.Add(new MacFlagsChange(downs[i].VirtualKey, left | NonCoalesced));
        }

        return new MacModifierEvents(downs, ups, held);
    }
}

/// <summary>A button's CoreGraphics event types and its <c>CGMouseButton</c> number.</summary>
internal readonly record struct MacMouseEvents(uint Down, uint Up, uint Dragged, uint Button);

/// <summary>One <c>kCGEventFlagsChanged</c> event: the modifier's virtual key and the flags it leaves set.</summary>
internal readonly record struct MacFlagsChange(ushort VirtualKey, ulong Flags);

/// <summary>The flagsChanged events before a button's down (<see cref="Downs"/>) and after it (<see cref="Ups"/>), and the flags the down carries (0: none set).</summary>
internal sealed record MacModifierEvents(IReadOnlyList<MacFlagsChange> Downs, IReadOnlyList<MacFlagsChange> Ups, ulong MouseDownFlags);
