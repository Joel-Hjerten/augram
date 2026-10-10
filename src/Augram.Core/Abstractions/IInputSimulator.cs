using Augram.Core.Capture;

namespace Augram.Core.Abstractions;

/// <summary>
/// Synthesises input (ADR-0002 §2): the click replay of a motionless press, a held-back press handed
/// back to the app (trigger combinations), the keyboard side of steps (hotkey, text, media keys), the wheel (Scroll) and a
/// hold remap's button output with its drags (F9, the <c>…RemapButton</c> members).
/// The engine worker and the command executor call it, never the hook thread, and every injected
/// down gets its up (checklist A19). Injected input comes back through the hook flagged as simulated and is ignored
/// there. <see cref="TypeText"/> is Unicode entry (layout independent; some games do not see it);
/// <see cref="TypeTextByKeys"/> presses the keys that produce the text on a US layout.
/// </summary>
public interface IInputSimulator
{
    /// <summary>
    /// True when a button pressed with <see cref="PressRemapButton"/> moves only with drags this simulator posts itself
    /// (<see cref="DragRemapButton"/>): while a hold remap holds a button output, the engine then swallows the physical moves
    /// and drags and re-posts each one. Read once, when the engine is built. True on macOS (learnings 0005: Blender does not
    /// move a posted Middle with physical right-drags); false on Windows, where the physical moves drive the posted button.
    /// </summary>
    bool RepostsRemapDrags { get; }
    /// <summary>A clean down+up pair of <paramref name="button"/> at the given screen position.</summary>
    SimulationResult Click(MouseButton button, int x, int y);

    /// <summary>
    /// <paramref name="button"/> down at the given position, not released: a held-back press handed back to the app (trigger
    /// combinations). The pointer moves there. Its release is a later <see cref="Release"/>, always (A19).
    /// </summary>
    SimulationResult Press(MouseButton button, int x, int y);

    /// <summary><paramref name="button"/> up where the pointer is: the release of a <see cref="Press"/>.</summary>
    SimulationResult Release(MouseButton button);

    /// <summary>
    /// A hold remap's button output (F9) down at the given position: <paramref name="modifiers"/> pressed (the left-hand keys,
    /// Ctrl, Alt, Shift, Win in that order), <paramref name="button"/> pressed with them held, the modifiers released, so the
    /// app reads them when the drag starts and nothing stays held through it (Blender's Shift + Middle pan). Only the four
    /// modifiers count. The button stays down until <see cref="ReleaseRemapButton"/>, always (A19).
    /// </summary>
    SimulationResult PressRemapButton(MouseButton button, KeyModifiers modifiers, int x, int y);

    /// <summary><paramref name="button"/> up where the pointer is now: the release of a <see cref="PressRemapButton"/>.</summary>
    SimulationResult ReleaseRemapButton(MouseButton button);

    /// <summary>
    /// One swallowed physical move re-posted as a drag of <paramref name="button"/>, which <see cref="PressRemapButton"/> holds:
    /// the pointer at (<paramref name="x"/>, <paramref name="y"/>), moved by (<paramref name="dx"/>, <paramref name="dy"/>)
    /// since the previous physical position, with the modifier keys held now. Called only when
    /// <see cref="RepostsRemapDrags"/>; a simulator that does not re-post answers <see cref="SimulationResult.Unsupported"/>.
    /// </summary>
    SimulationResult DragRemapButton(MouseButton button, int x, int y, int dx, int dy);

    /// <summary>Moves the pointer to the given screen position.</summary>
    SimulationResult MoveTo(int x, int y);

    /// <summary>
    /// Moves the pointer to the given screen position and turns the wheel <paramref name="notches"/> times in
    /// <paramref name="direction"/>, one standard notch each (120 on Windows, one line on macOS), so the window under that
    /// point scrolls. The pointer stays there. Keys held while scrolling are the caller's to press and release.
    /// </summary>
    SimulationResult Scroll(ScrollDirection direction, int notches, int x, int y);

    SimulationResult KeyPress(KeyCode key);

    SimulationResult KeyRelease(KeyCode key);

    /// <summary>
    /// Presses the modifiers, taps <paramref name="key"/>, releases the modifiers in reverse order. Each
    /// modifier in <paramref name="rightHand"/> is pressed with its right-hand key (RightAlt, RightControl…),
    /// the others with the left; a <paramref name="rightHand"/> bit outside <paramref name="modifiers"/> is ignored.
    /// </summary>
    SimulationResult Hotkey(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None);

    SimulationResult TypeText(string text);

    SimulationResult TypeTextByKeys(string text);
}
