using Augram.Core.Abstractions;
using Augram.Core.Capture;
using SharpHook;

namespace Augram.Engine.Input;

/// <summary>
/// <see cref="IInputSimulator"/> over SharpHook's <see cref="EventSimulator"/>. Thin and untested
/// (it needs a desktop) except for <see cref="ModifierKeys"/>, the left/right choice a hotkey presses;
/// everything above it is driven through a fake in tests. Injected events come
/// back through the hook with <c>IsEventSimulated</c> set and <see cref="SharpHookInputSource"/> drops
/// them, which is what keeps a replayed click from being captured again.
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

    public SharpHookInputSimulator()
        : this(new EventSimulator())
    {
    }

    public SharpHookInputSimulator(IEventSimulator simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        _simulator = simulator;
    }

    public SimulationResult Click(MouseButton button, int x, int y)
    {
        var hookButton = MouseButtonMap.ToHook(button);
        var (sx, sy) = ((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue));
        var press = _simulator.SimulateMousePress(sx, sy, hookButton);
        var release = _simulator.SimulateMouseRelease(sx, sy, hookButton);
        return Combine(press, release);
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
