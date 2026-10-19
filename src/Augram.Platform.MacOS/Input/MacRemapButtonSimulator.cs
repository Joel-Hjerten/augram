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
/// <item><see cref="RepostMove"/>: the same event as a plain <c>kCGEventMouseMoved</c>, for the moves the engine swallows while
/// a button trigger's chord holds a key output with modifiers (plan 0005): a passing hardware move carries the hardware's flags
/// (none) and resets the session's modifier state, so an app polling <c>flagsState(.combinedSessionState)</c> read the posted
/// modifiers as up (Iris's loupe, learnings 0005, "Synthetic modifiers and passing moves").</item>
/// </list>
/// Engine worker thread. No AppKit, so no main-thread hop and no autorelease pool. What is posted comes back through the hook
/// marked simulated, and since the Mac takes other programs' simulated input as real (2026-10-19, the Wacom pen; Engine
/// <c>ForeignInput.Take</c>) every mouse event posted here is announced first through <see cref="MacPostedInput"/> (the engine's
/// <c>OwnInjections</c>), one per event: the down of <see cref="PressRemapButton"/>, the up of <see cref="ReleaseRemapButton"/>,
/// the one drag of <see cref="DragRemapButton"/> and the one move of <see cref="RepostMove"/>. The hook claims and drops exactly
/// those; a re-posted drag taken as a real move would be swallowed and re-posted again without end (the spike relied on the drop
/// too). The flagsChanged events are keyboard events, which the hook drops simulated or not, and are not announced. An
/// announcement is made once the event exists, just before <c>CGEventPost</c>, which cannot fail, so nothing is withdrawn.
/// Posting is checked by Joel on his Mac (the Platform.MacOS README lists the checks); the announcements are tested with the
/// post replaced (the internal constructor), on a Mac only, since the events are built by CoreGraphics.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacRemapButtonSimulator : IInputSimulator
{
    private const uint HidEventTap = 0;

    private readonly IInputSimulator _inner;
    private readonly MacPostedInput? _announce;
    private readonly Action<nint> _post;

    public MacRemapButtonSimulator(IInputSimulator inner, MacPostedInput? announce = null)
        : this(inner, announce, static cgEvent => MacNative.CGEventPost(HidEventTap, cgEvent))
    {
    }

    /// <summary>Tests: <paramref name="post"/> stands in for <c>CGEventPost</c>; the events are still built and released.</summary>
    internal MacRemapButtonSimulator(IInputSimulator inner, MacPostedInput? announce, Action<nint> post)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(post);
        _inner = inner;
        _announce = announce;
        _post = post;
    }

    public bool RepostsRemapDrags => true;

    public SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y)
    {
        var mouse = MacRemapButtonEvents.For(button);
        var keys = MacRemapButtonEvents.Modifiers(modifiers);
        var result = SimulationResult.Success;
        foreach (var change in keys.Downs)
        {
            result = InputSimulation.Worst(result, PostFlagsChanged(change, keyDown: true));
        }

        result = InputSimulation.Worst(result, PostButton(mouse.Down, mouse.Button, Point(x, y), keys.MouseDownFlags, beforePost: () => _announce?.Press(button)));
        foreach (var change in keys.Ups)
        {
            result = InputSimulation.Worst(result, PostFlagsChanged(change, keyDown: false));
        }

        return result;
    }

    public SimulationResult ReleaseRemapButton(MouseButton button)
    {
        var mouse = MacRemapButtonEvents.For(button);
        var result = MacPointer.TryRead(out var at) ? PostButton(mouse.Up, mouse.Button, at, flags: 0, beforePost: () => _announce?.Release(button)) : SimulationResult.Failed;
        return result == SimulationResult.Success ? result : _inner.ReleaseRemapButton(button);
    }

    public SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy)
    {
        var mouse = MacRemapButtonEvents.For(button);
        return PostMotion(mouse.Dragged, mouse.Button, x, y, dx, dy);
    }

    /// <summary>A plain move, built as a drag is (the button number is ignored for <c>kCGEventMouseMoved</c>).</summary>
    public SimulationResult RepostMove(int x, int y, int dx, int dy) => PostMotion(MacRemapButtonEvents.MouseMoved, 0, x, y, dx, dy);

    public SimulationResult Click(MouseButton button, int x, int y) => _inner.Click(button, x, y);

    public SimulationResult Press(MouseButton button, int x, int y) => _inner.Press(button, x, y);

    public SimulationResult Release(MouseButton button) => _inner.Release(button);

    public SimulationResult MoveTo(int x, int y) => _inner.MoveTo(x, y);

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => _inner.Scroll(direction, notches, x, y);

    public SimulationResult KeyPress(KeyCode key) => _inner.KeyPress(key);

    public SimulationResult KeyRelease(KeyCode key) => _inner.KeyRelease(key);

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) => _inner.Hotkey(modifiers, key, rightHand);

    public SimulationResult TypeText(string text) => _inner.TypeText(text);

    public SimulationResult TypeTextByKeys(string text) => _inner.TypeTextByKeys(text);

    private static MacNative.CGPoint Point(int x, int y) => new() { X = x, Y = y };

    /// <summary>A drag or a move at the position, <c>kCGMouseEventDeltaX</c> and <c>DeltaY</c> set, with the session's modifier flags as they are now; announced as one move.</summary>
    private SimulationResult PostMotion(uint type, uint button, int x, int y, int dx, int dy)
    {
        var cgEvent = MacNative.CGEventCreateMouseEvent(0, type, Point(x, y), button);
        if (cgEvent == 0)
        {
            return SimulationResult.Failed;
        }

        MacNative.CGEventSetIntegerValueField(cgEvent, MacNative.CGMouseEventDeltaX, dx);
        MacNative.CGEventSetIntegerValueField(cgEvent, MacNative.CGMouseEventDeltaY, dy);
        MacNative.CGEventSetFlags(cgEvent, MacNative.CGEventSourceFlagsState(MacNative.CGEventSourceStateCombinedSession));
        _announce?.Move();
        return Post(cgEvent);
    }

    /// <summary>A keyboard event of the modifier's key retyped as flagsChanged, carrying the flags it leaves set.</summary>
    private SimulationResult PostFlagsChanged(MacFlagsChange change, bool keyDown)
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
    private SimulationResult PostButton(uint type, uint button, MacNative.CGPoint at, ulong flags, Action? beforePost = null)
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

    private SimulationResult Post(nint cgEvent)
    {
        _post(cgEvent);
        Cf.Release(cgEvent);
        return SimulationResult.Success;
    }
}
