using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Input;

namespace Augram.Engine.Execution;

/// <summary>
/// A stroke-button click that was held back as a click trigger and fired nothing: it goes to the app after all, with the keys
/// that were pressed during the press (and swallowed) held around it (Joel, 2026-10-09: "pass it through, with the modifier
/// still held"; Before keys are still physically held). So Shift+right-click reaches Explorer as its extended menu without
/// SP.net's "Shift+Right Click" command. Injected by the worker (no mapping) or the executor (after resolving); both down and
/// up of every key and of the button are injected, so nothing is left held (A19).
/// </summary>
internal sealed record ClickRelay(MouseButton Button, int X, int Y, KeyModifiers AfterKeys)
{
    public SimulationResult Run(IInputSimulator simulator) => Replay(simulator, Button, X, Y, AfterKeys);

    /// <summary>
    /// <paramref name="keys"/> down (left-hand keys, Ctrl, Alt, Shift, Win), the click, a Ctrl tap when Alt or Win is among them
    /// (so Windows sees no lone Alt or Win), the keys up in reverse; the worst result of all of it.
    /// </summary>
    public static SimulationResult Replay(IInputSimulator simulator, MouseButton button, int x, int y, KeyModifiers keys)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        if (keys == KeyModifiers.None)
        {
            return simulator.Click(button, x, y);
        }

        var held = SharpHookInputSimulator.ModifierKeys(keys, KeyModifiers.None);
        var result = SimulationResult.Success;
        foreach (var key in held)
        {
            result = Worst(result, simulator.KeyPress(key));
        }

        result = Worst(result, simulator.Click(button, x, y));
        if ((keys & (KeyModifiers.Alt | KeyModifiers.Meta)) != 0 && (keys & KeyModifiers.Control) == 0)
        {
            result = Worst(result, simulator.KeyPress(KeyCode.LeftControl));
            result = Worst(result, simulator.KeyRelease(KeyCode.LeftControl));
        }

        for (var i = held.Count - 1; i >= 0; i--)
        {
            result = Worst(result, simulator.KeyRelease(held[i]));
        }

        return result;
    }

    private static SimulationResult Worst(SimulationResult a, SimulationResult b) => a >= b ? a : b;
}
