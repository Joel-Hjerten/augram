using Augram.Core.Capture;

namespace Augram.Core.Abstractions;

/// <summary>
/// Synthesises input (ADR-0002 §2): the click replay of a motionless press, and the keyboard side
/// of steps (hotkey, text, media keys). Only the engine worker calls it, never the hook thread
/// (checklist A19). Injected input comes back through the hook flagged as simulated and is ignored
/// there. <see cref="TypeText"/> is Unicode entry (layout independent; some games do not see it);
/// <see cref="TypeTextByKeys"/> presses the keys that produce the text on a US layout.
/// </summary>
public interface IInputSimulator
{
    /// <summary>A clean down+up pair of <paramref name="button"/> at the given screen position.</summary>
    SimulationResult Click(MouseButton button, int x, int y);

    SimulationResult KeyPress(KeyCode key);

    SimulationResult KeyRelease(KeyCode key);

    /// <summary>Presses the modifiers, taps <paramref name="key"/>, releases the modifiers in reverse order.</summary>
    SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key);

    SimulationResult TypeText(string text);

    SimulationResult TypeTextByKeys(string text);
}
