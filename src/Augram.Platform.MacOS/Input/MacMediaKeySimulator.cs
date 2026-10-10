using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The macOS <see cref="IInputSimulator"/>: everything goes to the toolkit's simulator except a bare press or release of a
/// volume or playback key, which the toolkit posts as a key code that macOS ignores (Joel, 2026-10-09: Volume Down fired,
/// the volume did not move). Those are posted as the system-defined events the keyboard's own media keys send
/// (<see cref="MacMediaKeys"/>) through <c>CGEventPost</c> at the HID tap, the same permission as any injected input. Runs on
/// the command executor thread inside an autorelease pool (<c>NSEvent</c> creation needs no main thread). A media key inside
/// a <see cref="Hotkey"/> still goes to the toolkit: no command does that.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacMediaKeySimulator(IInputSimulator inner) : IInputSimulator
{
    private const uint HidEventTap = 0;

    public SimulationResult Click(MouseButton button, int x, int y) => inner.Click(button, x, y);

    public SimulationResult Press(MouseButton button, int x, int y) => inner.Press(button, x, y);

    public SimulationResult Release(MouseButton button) => inner.Release(button);

    public bool RepostsRemapDrags => inner.RepostsRemapDrags;

    public SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y) => inner.PressRemapButton(button, modifiers, x, y);

    public SimulationResult ReleaseRemapButton(MouseButton button) => inner.ReleaseRemapButton(button);

    public SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy) => inner.DragRemapButton(button, x, y, dx, dy);

    public SimulationResult MoveTo(int x, int y) => inner.MoveTo(x, y);

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y) => inner.Scroll(direction, notches, x, y);

    public SimulationResult KeyPress(KeyCode key) => MacMediaKeys.TryMap(key, out var keyType) ? Post(keyType, pressed: true) : inner.KeyPress(key);

    public SimulationResult KeyRelease(KeyCode key) => MacMediaKeys.TryMap(key, out var keyType) ? Post(keyType, pressed: false) : inner.KeyRelease(key);

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None) => inner.Hotkey(modifiers, key, rightHand);

    public SimulationResult TypeText(string text) => inner.TypeText(text);

    public SimulationResult TypeTextByKeys(string text) => inner.TypeTextByKeys(text);

    private static SimulationResult Post(int keyType, bool pressed)
    {
        var eventClass = ObjC.Class("NSEvent");
        if (eventClass == 0)
        {
            return SimulationResult.Unsupported;
        }

        using var pool = ObjC.Pool();
        var nsEvent = MacNative.SendSystemDefinedEvent(
            eventClass,
            ObjC.Selector("otherEventWithType:location:modifierFlags:timestamp:windowNumber:context:subtype:data1:data2:"),
            MacMediaKeys.SystemDefinedType,
            default,
            MacMediaKeys.ModifierFlags(pressed),
            0,
            0,
            0,
            MacMediaKeys.AuxControlButtons,
            MacMediaKeys.Data1(keyType, pressed),
            -1);
        var cgEvent = nsEvent == 0 ? 0 : MacNative.SendPtr(nsEvent, ObjC.Selector("CGEvent"));
        if (cgEvent == 0)
        {
            return SimulationResult.Failed;
        }

        MacNative.CGEventPost(HidEventTap, cgEvent);
        return SimulationResult.Success;
    }
}
