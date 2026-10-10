using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The macOS <see cref="IInputSimulator"/>'s hold remap half (plan 0002 step 3a, learnings 0005): wraps the rest of the chain
/// (<c>EngineModule.CreateSimulator</c>: SharpHook's simulator inside <see cref="MacMediaKeySimulator"/>) and posts a hold
/// remap's button output and its drags through CoreGraphics itself, at the HID tap with no event source, as the spike that
/// navigated Blender did. Everything else goes to the inner simulator.
/// <list type="bullet">
/// <item><see cref="PressRemapButton"/>: a <c>kCGEventFlagsChanged</c> event per modifier (<see cref="MacRemapButtonEvents"/>),
/// the button's down at the position with the modifiers' flags set on it (<c>CGEventSetFlags</c>), then a flagsChanged per
/// modifier without it. Shift and Ctrl pressed through SharpHook never reached Blender with a posted Middle (the spike's first
/// run: Ctrl + Middle orbited instead of zooming).</item>
/// <item><see cref="ReleaseRemapButton"/>: the button's up where the pointer is now; if CoreGraphics cannot build it, the inner
/// simulator's release, so the button is never left down (A19).</item>
/// <item><see cref="DragRemapButton"/>: the button's dragged event at the position with <c>kCGMouseEventDeltaX</c> and
/// <c>DeltaY</c> (fields 4 and 5) set to the deltas and the session's modifier flags as they are now.
/// <see cref="RepostsRemapDrags"/> is true: macOS does not move a posted Middle with physical right-drags (left-drags it
/// does), so the engine swallows every physical move while a button output is held and re-posts it here.</item>
/// </list>
/// Engine worker thread. No AppKit, so no main-thread hop and no autorelease pool. What is posted comes back through the hook
/// marked simulated and is dropped there (the spike relied on that too: otherwise a re-posted drag would be swallowed again).
/// A release posted here is announced first through <paramref name="announceRelease"/> (the engine's <c>OwnButtonInjections</c>),
/// so the hook does not take it for another program's release (plan 0005 decision 10).
/// <b>Compiled, not yet run on a Mac</b> (the Platform.MacOS README lists the checks).
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacRemapButtonSimulator(IInputSimulator inner, Action<MouseButton>? announceRelease = null) : IInputSimulator
{
    private const uint HidEventTap = 0;

    public bool RepostsRemapDrags => true;

    public SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y)
    {
        var mouse = MacRemapButtonEvents.For(button);
        var keys = MacRemapButtonEvents.Modifiers(modifiers);
        var result = SimulationResult.Success;
        foreach (var change in keys.Downs)
        {
            result = Worst(result, PostFlagsChanged(change, keyDown: true));
        }

        result = Worst(result, PostButton(mouse.Down, mouse.Button, Point(x, y), keys.MouseDownFlags));
        foreach (var change in keys.Ups)
        {
            result = Worst(result, PostFlagsChanged(change, keyDown: false));
        }

        return result;
    }

    public SimulationResult ReleaseRemapButton(MouseButton button)
    {
        var mouse = MacRemapButtonEvents.For(button);
        var result = TryPointer(out var at) ? PostButton(mouse.Up, mouse.Button, at, flags: 0, beforePost: () => announceRelease?.Invoke(button)) : SimulationResult.Failed;
        return result == SimulationResult.Success ? result : inner.ReleaseRemapButton(button);
    }

    public SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy)
    {
        var mouse = MacRemapButtonEvents.For(button);
        var cgEvent = MacNative.CGEventCreateMouseEvent(0, mouse.Dragged, Point(x, y), mouse.Button);
        if (cgEvent == 0)
        {
            return SimulationResult.Failed;
        }

        MacNative.CGEventSetIntegerValueField(cgEvent, MacNative.CGMouseEventDeltaX, dx);
        MacNative.CGEventSetIntegerValueField(cgEvent, MacNative.CGMouseEventDeltaY, dy);
        MacNative.CGEventSetFlags(cgEvent, MacNative.CGEventSourceFlagsState(MacNative.CGEventSourceStateCombinedSession));
        return Post(cgEvent);
    }

    public SimulationResult Click(MouseButton button, int x, int y) => inner.Click(button, x, y);

    public SimulationResult Press(MouseButton button, int x, int y) => inner.Press(button, x, y);

    public SimulationResult Release(MouseButton button) => inner.Release(button);

    public SimulationResult MoveTo(int x, int y) => inner.MoveTo(x, y);

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => inner.Scroll(direction, notches, x, y);

    public SimulationResult KeyPress(KeyCode key) => inner.KeyPress(key);

    public SimulationResult KeyRelease(KeyCode key) => inner.KeyRelease(key);

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) => inner.Hotkey(modifiers, key, rightHand);

    public SimulationResult TypeText(string text) => inner.TypeText(text);

    public SimulationResult TypeTextByKeys(string text) => inner.TypeTextByKeys(text);

    private static MacNative.CGPoint Point(int x, int y) => new() { X = x, Y = y };

    /// <summary>A keyboard event of the modifier's key retyped as flagsChanged, carrying the flags it leaves set.</summary>
    private static SimulationResult PostFlagsChanged(MacFlagsChange change, bool keyDown)
    {
        var cgEvent = MacNative.CGEventCreateKeyboardEvent(0, change.VirtualKey, keyDown ? (byte)1 : (byte)0);
        if (cgEvent == 0)
        {
            return SimulationResult.Failed;
        }

        MacNative.CGEventSetType(cgEvent, MacRemapButtonEvents.FlagsChanged);
        MacNative.CGEventSetFlags(cgEvent, change.Flags);
        return Post(cgEvent);
    }

    /// <summary>A button's down or up (click count 1); <paramref name="flags"/> set on it unless zero; <paramref name="beforePost"/> runs once the event exists, just before it is posted.</summary>
    private static SimulationResult PostButton(uint type, uint button, MacNative.CGPoint at, ulong flags, Action? beforePost = null)
    {
        var cgEvent = MacNative.CGEventCreateMouseEvent(0, type, at, button);
        if (cgEvent == 0)
        {
            return SimulationResult.Failed;
        }

        MacNative.CGEventSetIntegerValueField(cgEvent, MacNative.CGMouseEventClickState, 1);
        if (flags != 0)
        {
            MacNative.CGEventSetFlags(cgEvent, flags);
        }

        beforePost?.Invoke();
        return Post(cgEvent);
    }

    /// <summary>Where the pointer is now: a blank event carries the current location (as <see cref="MacCursorProbe"/> reads it).</summary>
    private static bool TryPointer(out MacNative.CGPoint at)
    {
        var blank = MacNative.CGEventCreate(0);
        if (blank == 0)
        {
            at = default;
            return false;
        }

        try
        {
            at = MacNative.CGEventGetLocation(blank);
            return true;
        }
        finally
        {
            Cf.Release(blank);
        }
    }

    private static SimulationResult Post(nint cgEvent)
    {
        MacNative.CGEventPost(HidEventTap, cgEvent);
        Cf.Release(cgEvent);
        return SimulationResult.Success;
    }

    private static SimulationResult Worst(SimulationResult a, SimulationResult b) => a >= b ? a : b;
}
