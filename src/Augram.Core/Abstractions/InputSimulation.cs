namespace Augram.Core.Abstractions;

/// <summary>
/// The two rules every <see cref="IInputSimulator"/> caller and implementation shares: how several injections' results
/// combine into one (<see cref="Worst"/>), and keys held around an injection (<see cref="WithModifiersHeld"/>, and its two
/// halves for a hold that spans two calls). Injection order is behaviour (Blender reads the modifiers at the button's down),
/// so each helper injects in exactly the order its summary gives. Never on the hook thread.
/// </summary>
public static class InputSimulation
{
    /// <summary>
    /// The result of several injections: the later <see cref="SimulationResult"/> member wins (Unsupported over Failed over
    /// Success), so one bad key makes the whole call report it.
    /// </summary>
    public static SimulationResult Worst(SimulationResult a, SimulationResult b) => a >= b ? a : b;

    /// <summary>
    /// <paramref name="keys"/> pressed in order, <paramref name="action"/>, the keys released in reverse; the worst result of
    /// all of it. Every key is pressed and released whatever an earlier injection answered.
    /// </summary>
    public static SimulationResult WithModifiersHeld(this IInputSimulator simulator, IReadOnlyList<KeyCode> keys, Func<SimulationResult> action)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(action);
        var result = simulator.PressKeys(keys);
        result = Worst(result, action());
        return Worst(result, simulator.ReleaseKeys(keys));
    }

    /// <summary><paramref name="keys"/> pressed in order; the worst result. The first half of <see cref="WithModifiersHeld"/>, for a hold whose release comes later (a hold remap's key output).</summary>
    public static SimulationResult PressKeys(this IInputSimulator simulator, IReadOnlyList<KeyCode> keys)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(keys);
        var result = SimulationResult.Success;
        for (var i = 0; i < keys.Count; i++)
        {
            result = Worst(result, simulator.KeyPress(keys[i]));
        }

        return result;
    }

    /// <summary><paramref name="keys"/> released in reverse order; the worst result. The second half of <see cref="WithModifiersHeld"/>.</summary>
    public static SimulationResult ReleaseKeys(this IInputSimulator simulator, IReadOnlyList<KeyCode> keys)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(keys);
        var result = SimulationResult.Success;
        for (var i = keys.Count - 1; i >= 0; i--)
        {
            result = Worst(result, simulator.KeyRelease(keys[i]));
        }

        return result;
    }
}
