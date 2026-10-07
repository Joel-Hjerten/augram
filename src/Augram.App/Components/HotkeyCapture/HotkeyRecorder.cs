using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// Turns the key events of one capture into F5's "modifier set + one key". Modifier keys only change
/// what is held; a non-modifier press completes a combination from the modifiers held then (tracked
/// here, plus the mask the event reported, which covers a modifier held since before the capture
/// began). The latest combination wins, so a wrong press is fixed by pressing again. A modifier is never
/// the key: a lone Win or Alt is picked from the form's dropdown instead. Keys without a name are ignored.
/// </summary>
public sealed class HotkeyRecorder
{
    public const string PendingSuffix = "+…";

    public KeyModifiers Held { get; private set; }

    public KeyModifiers Modifiers { get; private set; }

    public KeyCode Key { get; private set; }

    public bool HasCombination => Key != KeyCode.None;

    /// <summary>"Ctrl+Shift+T" once a key is pressed, "Ctrl+Shift+…" while only modifiers are held, null before anything.</summary>
    public string? LiveText => HasCombination
        ? HotkeyText.Format(Modifiers, Key)
        : Held != KeyModifiers.None ? HotkeyText.Format(Held, KeyCode.None) + PendingSuffix : null;

    public void Reset()
    {
        Held = KeyModifiers.None;
        Modifiers = KeyModifiers.None;
        Key = KeyCode.None;
    }

    public void Down(KeyCode key, KeyModifiers reported)
    {
        var modifier = HotkeyKeys.ModifierOf(key);
        if (modifier != KeyModifiers.None)
        {
            Held |= modifier;
            return;
        }

        if (key == KeyCode.None)
        {
            return;
        }

        Modifiers = (Held | reported) & HotkeyKeys.AllModifiers;
        Key = key;
    }

    public void Up(KeyCode key) => Held &= ~HotkeyKeys.ModifierOf(key);
}
