namespace Augram.Core.Abstractions;

/// <summary>
/// The keyboard layouts the OS has, as the Hotkey step sees them (plan 0011): which one is active, which are installed, and
/// what a key prints under the active one. One home for "which layout" and "what does this key print". Implemented by
/// Platform.MacOS (Text Input Sources) and Platform.Windows (the foreground thread's layout); <see cref="NullKeyboardLayouts"/>
/// knows none. <see cref="Active"/> is read once per run of a Hotkey step that holds layout shortcuts, on the command executor
/// thread; <see cref="CharacterOf"/> from the UI thread for key names and from the executor's log line. An adapter that needs
/// its own thread marshals inside; every member answers at once and never throws for an expected failure.
/// </summary>
public interface IKeyboardLayouts
{
    /// <summary>The layout the next keystroke lands in, or null when the platform cannot say.</summary>
    KeyboardLayout? Active { get; }

    /// <summary>The layouts the user can switch between, in the OS's order; empty when the platform cannot say.</summary>
    IReadOnlyList<KeyboardLayout> Installed { get; }

    /// <summary>
    /// What <paramref name="key"/> prints under the active layout with no modifier held ("ö" for the key the US layout calls
    /// Semicolon), or null when the key prints nothing (F-keys, arrows, a dead key) or the platform cannot say. Display only:
    /// the stored key stays the <see cref="KeyCode"/>.
    /// </summary>
    string? CharacterOf(KeyCode key);
}
