using Augram.Core.Abstractions;
using Augram.Core.Capture;
using SharpHook;
using WheelAxis = SharpHook.Data.MouseWheelScrollDirection;
using WheelUnit = SharpHook.Data.MouseWheelScrollType;

namespace Augram.Engine.Input;

/// <summary>
/// <see cref="IInputSimulator"/> over SharpHook's <see cref="EventSimulator"/>. Thin and untested against a desktop
/// except for <see cref="ModifierKeys"/>, the left/right choice a hotkey presses, and <see cref="Notch"/>, one wheel
/// notch per platform (<see cref="Scroll"/> itself is tested over SharpHook's <c>TestGlobalHook</c>, which posts nothing);
/// everything above it is driven through a fake in tests. Injected events come
/// back through the hook with <c>IsEventSimulated</c> set and <see cref="SharpHookInputSource"/> drops
/// them, which is what keeps a replayed click from being captured again. Wheel notches are the exception: the hook takes
/// other programs' simulated wheel events, so <see cref="Scroll"/> announces its vertical notches to
/// <see cref="OwnWheelInjections"/> first and the hook drops exactly those.
/// </summary>
public sealed class SharpHookInputSimulator : IInputSimulator
{
    private static readonly (KeyModifiers Flag, KeyCode Left, KeyCode Right)[] ModifierKeyPairs =
    [
        (KeyModifiers.Control, KeyCode.LeftControl, KeyCode.RightControl),
        (KeyModifiers.Alt, KeyCode.LeftAlt, KeyCode.RightAlt),
        (KeyModifiers.Shift, KeyCode.LeftShift, KeyCode.RightShift),
        (KeyModifiers.Meta, KeyCode.LeftMeta, KeyCode.RightMeta),
    ];

    private readonly IEventSimulator _simulator;
    private readonly OwnWheelInjections? _ownWheel;

    public SharpHookInputSimulator(OwnWheelInjections? ownWheel = null)
        : this(new EventSimulator(), ownWheel)
    {
    }

    public SharpHookInputSimulator(IEventSimulator simulator, OwnWheelInjections? ownWheel = null)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        _simulator = simulator;
        _ownWheel = ownWheel;
    }

    public SimulationResult Click(MouseButton button, int x, int y)
    {
        var hookButton = MouseButtonMap.ToHook(button);
        var (sx, sy) = Point(x, y);
        var press = _simulator.SimulateMousePress(sx, sy, hookButton);
        var release = _simulator.SimulateMouseRelease(sx, sy, hookButton);
        return Combine(press, release);
    }

    public SimulationResult Press(MouseButton button, int x, int y)
    {
        var (sx, sy) = Point(x, y);
        return Translate(_simulator.SimulateMousePress(sx, sy, MouseButtonMap.ToHook(button)));
    }

    public SimulationResult Release(MouseButton button) => Translate(_simulator.SimulateMouseRelease(MouseButtonMap.ToHook(button)));

    public SimulationResult MoveTo(int x, int y)
    {
        var (sx, sy) = Point(x, y);
        return Translate(_simulator.SimulateMouseMovement(sx, sy));
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
            _ownWheel?.Expect(notches);
        }

        var result = SimulationResult.Success;
        for (var i = 0; i < notches; i++)
        {
            result = Worst(result, Translate(_simulator.SimulateMouseWheel(rotation, axis, type)));
        }

        return result;
    }

    public SimulationResult KeyPress(KeyCode key) => WithKey(key, _simulator.SimulateKeyPress);

    public SimulationResult KeyRelease(KeyCode key) => WithKey(key, _simulator.SimulateKeyRelease);

    public SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None)
    {
        var held = ModifierKeys(modifiers, rightHand);
        var result = SimulationResult.Success;
        foreach (var modifier in held)
        {
            result = Worst(result, KeyPress(modifier));
        }

        result = Worst(result, KeyPress(key));
        result = Worst(result, KeyRelease(key));
        for (var i = held.Count - 1; i >= 0; i--)
        {
            result = Worst(result, KeyRelease(held[i]));
        }

        return result;
    }

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
                result = Worst(result, SimulationResult.Unsupported);
                continue;
            }

            result = Worst(result, shift ? Hotkey(KeyModifiers.Shift, key) : Combine(KeyPress(key), KeyRelease(key)));
        }

        return result;
    }

    /// <summary>
    /// The keys <see cref="Hotkey"/> holds, in press order (Ctrl, Alt, Shift, Win): the right-hand key for a
    /// modifier in <paramref name="rightHand"/>, the left one otherwise; right-hand bits outside the modifiers press nothing.
    /// </summary>
    internal static List<KeyCode> ModifierKeys(KeyModifiers modifiers, KeyModifiers rightHand)
    {
        var keys = new List<KeyCode>(ModifierKeyPairs.Length);
        foreach (var (flag, left, right) in ModifierKeyPairs)
        {
            if ((modifiers & flag) != 0)
            {
                keys.Add((rightHand & flag) != 0 ? right : left);
            }
        }

        return keys;
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

    private static (short X, short Y) Point(int x, int y)
        => ((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue));

    private static SimulationResult WithKey(KeyCode key, Func<SharpHook.Data.KeyCode, SharpHook.Data.UioHookResult> simulate)
    {
        var hookKey = KeyCodeMap.ToHook(key);
        return hookKey == SharpHook.Data.KeyCode.VcUndefined ? SimulationResult.Unsupported : Translate(simulate(hookKey));
    }

    private static SimulationResult Translate(SharpHook.Data.UioHookResult result) =>
        result == SharpHook.Data.UioHookResult.Success ? SimulationResult.Success : SimulationResult.Failed;

    private static SimulationResult Combine(SharpHook.Data.UioHookResult first, SharpHook.Data.UioHookResult second) => Worst(Translate(first), Translate(second));

    private static SimulationResult Combine(SimulationResult first, SimulationResult second) => Worst(first, second);

    /// <summary>Failed outranks Unsupported outranks Success: one bad key makes the whole call report it.</summary>
    private static SimulationResult Worst(SimulationResult a, SimulationResult b) => a >= b ? a : b;
}
