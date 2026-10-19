using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;
using SharpHook;
using WheelAxis = SharpHook.Data.MouseWheelScrollDirection;
using WheelUnit = SharpHook.Data.MouseWheelScrollType;

namespace Augram.Engine.Input;

/// <summary>
/// <see cref="IInputSimulator"/> over SharpHook's <see cref="EventSimulator"/>. Thin and untested against a desktop
/// except for <see cref="Notch"/>, one wheel notch per platform (<see cref="Scroll"/> itself is tested over SharpHook's
/// <c>TestGlobalHook</c>, which posts nothing); everything above it is driven through a fake in tests. The keys a hotkey or a
/// hold remap button output holds are Core's (<see cref="HotkeyKeys.Keys"/>, held through
/// <see cref="InputSimulation.WithModifiersHeld"/>). Injected events come back through the hook with <c>IsEventSimulated</c>
/// set, and <see cref="SharpHookInputSource"/> must drop Augram's own while it takes other programs' (wheel events everywhere;
/// releases, as "released elsewhere", on Windows, plan 0005 decision 10; presses, releases and moves on macOS,
/// <see cref="ForeignInput.Take"/>). So every mouse event posted here is announced to <see cref="OwnInjections"/> first, one per
/// hook event it becomes (<see cref="Scroll"/>'s vertical notches; a click's press and release; a press; a release; a move), and
/// withdrawn when the post fails; the hook claims and drops exactly those. Keyboard events are not announced: the hook drops
/// every simulated key event.
/// </summary>
public sealed class SharpHookInputSimulator : IInputSimulator
{
    private readonly IEventSimulator _simulator;
    private readonly OwnInjections? _own;

    public SharpHookInputSimulator(OwnInjections? own = null)
        : this(new EventSimulator(), own)
    {
    }

    public SharpHookInputSimulator(IEventSimulator simulator, OwnInjections? own = null)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        _simulator = simulator;
        _own = own;
    }

    public SimulationResult Click(MouseButton button, int x, int y)
    {
        var hookButton = MouseButtonMap.ToHook(button);
        var (sx, sy) = Point(x, y);
        // One press and one release event at the hook (the click event libuiohook adds is a hook-side notion, never posted).
        var press = Announced(OwnInjections.PressSlotOf(button), () => _simulator.SimulateMousePress(sx, sy, hookButton));
        var release = Announced(OwnInjections.ReleaseSlotOf(button), () => _simulator.SimulateMouseRelease(sx, sy, hookButton));
        return Combine(press, release);
    }

    public SimulationResult Press(MouseButton button, int x, int y)
    {
        var (sx, sy) = Point(x, y);
        return Translate(Announced(OwnInjections.PressSlotOf(button), () => _simulator.SimulateMousePress(sx, sy, MouseButtonMap.ToHook(button))));
    }

    public SimulationResult Release(MouseButton button) => Translate(Announced(OwnInjections.ReleaseSlotOf(button), () => _simulator.SimulateMouseRelease(MouseButtonMap.ToHook(button))));

    /// <summary>False: on Windows the physical moves drive a posted button as they are (the AutoHotkey script proves it).</summary>
    public bool RepostsRemapDrags => false;

    /// <summary>The left modifier keys down, <see cref="Press"/>, the keys up in reverse: exactly what the worker posted itself before plan 0002 step 3a.</summary>
    public SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y)
        => this.WithModifiersHeld(HotkeyKeys.LeftKeys(modifiers), () => Press(button, x, y));

    public SimulationResult ReleaseRemapButton(MouseButton button) => Release(button);

    /// <summary>Not offered (<see cref="RepostsRemapDrags"/> is false): the engine never calls it here.</summary>
    public SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy) => SimulationResult.Unsupported;

    /// <summary>Not offered (<see cref="RepostsRemapDrags"/> is false): the engine never calls it here.</summary>
    public SimulationResult RepostMove(int x, int y, int dx, int dy) => SimulationResult.Unsupported;

    public SimulationResult MoveTo(int x, int y)
    {
        var (sx, sy) = Point(x, y);
        return Translate(Announced(InjectionSlot.Move, () => _simulator.SimulateMouseMovement(sx, sy)));
    }

    public SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y)
    {
        var moved = MoveTo(x, y);
        if (moved != SimulationResult.Success)
        {
            return moved;
        }

        var (rotation, axis, type) = Notch(direction, OperatingSystem.IsMacOS());
        if (axis == WheelAxis.Vertical)
        {
            _own?.Expect(InjectionSlot.Wheel, notches);
        }

        var result = SimulationResult.Success;
        for (var i = 0; i < notches; i++)
        {
            result = InputSimulation.Worst(result, Translate(_simulator.SimulateMouseWheel(rotation, axis, type)));
        }

        return result;
    }

    public SimulationResult KeyPress(KeyCode key) => WithKey(key, _simulator.SimulateKeyPress);

    public SimulationResult KeyRelease(KeyCode key) => WithKey(key, _simulator.SimulateKeyRelease);

    /// <summary>The modifiers' keys down (<see cref="HotkeyKeys.Keys"/>: left or right hand), <paramref name="key"/> pressed and released, the modifiers up in reverse.</summary>
    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None)
        => this.WithModifiersHeld(HotkeyKeys.Keys(modifiers, rightHand), () => Combine(KeyPress(key), KeyRelease(key)));

    public SimulationResult TypeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? SimulationResult.Success : Translate(_simulator.SimulateTextEntry(text));
    }

    public SimulationResult TypeTextByKeys(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = SimulationResult.Success;
        foreach (var c in text)
        {
            if (!AsciiKeyLayout.TryGetKey(c, out var key, out var shift))
            {
                result = InputSimulation.Worst(result, SimulationResult.Unsupported);
                continue;
            }

            result = InputSimulation.Worst(result, shift ? Hotkey(KeyModifiers.Shift, key) : Combine(KeyPress(key), KeyRelease(key)));
        }

        return result;
    }

    /// <summary>
    /// One wheel notch as SharpHook takes it (positive is up or left): 120 per notch on Windows (<c>WHEEL_DELTA</c>, what
    /// a wheel click sends; the scroll type is ignored there), one line on macOS (<c>BlockScroll</c> is the line unit
    /// there; pixel units would barely move).
    /// </summary>
    internal static (short Rotation, WheelAxis Axis, WheelUnit Type) Notch(ScrollDirection direction, bool macOS)
    {
        short step = macOS ? (short)1 : (short)120;
        var type = macOS ? WheelUnit.BlockScroll : WheelUnit.UnitScroll;
        return direction switch
        {
            ScrollDirection.Up => (step, WheelAxis.Vertical, type),
            ScrollDirection.Down => ((short)-step, WheelAxis.Vertical, type),
            ScrollDirection.Left => (step, WheelAxis.Horizontal, type),
            ScrollDirection.Right => ((short)-step, WheelAxis.Horizontal, type),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a scroll direction."),
        };
    }

    /// <summary>One mouse event of ours, announced into <paramref name="slot"/> before it is posted so the hook does not take it for another program's; withdrawn when the post fails.</summary>
    private SharpHook.Data.UioHookResult Announced(InjectionSlot slot, Func<SharpHook.Data.UioHookResult> post)
    {
        _own?.Expect(slot);
        var result = post();
        if (result != SharpHook.Data.UioHookResult.Success)
        {
            _own?.Withdraw(slot);
        }

        return result;
    }

    private static (short X, short Y) Point(int x, int y)
        => ((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue));

    private static SimulationResult WithKey(KeyCode key, Func<SharpHook.Data.KeyCode, SharpHook.Data.UioHookResult> simulate)
    {
        var hookKey = KeyCodeMap.ToHook(key);
        return hookKey == SharpHook.Data.KeyCode.VcUndefined ? SimulationResult.Unsupported : Translate(simulate(hookKey));
    }

    private static SimulationResult Translate(SharpHook.Data.UioHookResult result) =>
        result == SharpHook.Data.UioHookResult.Success ? SimulationResult.Success : SimulationResult.Failed;

    private static SimulationResult Combine(SharpHook.Data.UioHookResult first, SharpHook.Data.UioHookResult second) => InputSimulation.Worst(Translate(first), Translate(second));

    private static SimulationResult Combine(SimulationResult first, SimulationResult second) => InputSimulation.Worst(first, second);
}
