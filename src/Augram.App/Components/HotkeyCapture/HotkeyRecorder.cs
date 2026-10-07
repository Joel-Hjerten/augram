using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// Turns the key events of one capture into F5's "modifier set + one key". Modifier keys only change
/// what is held; a non-modifier press completes a combination from the modifiers held then (tracked
/// here, plus the mask the event reported, which covers a modifier held since before the capture
/// began). Each held modifier remembers its side: one held on the right only is recorded as
/// <see cref="RightHand"/> ("RAlt+F9"); held on both sides, or known only from the reported mask, it is
/// plain. The latest combination wins, so a wrong press is fixed by pressing again. A modifier is never
/// the key: a lone Win or Alt is picked from the form's dropdown instead. Keys without a name are ignored.
/// </summary>
public sealed class HotkeyRecorder
{
    public const string PendingSuffix = "+…";

    private KeyModifiers _heldLeft;
    private KeyModifiers _heldRight;

    /// <summary>The modifiers held now, either side.</summary>
    public KeyModifiers Held => _heldLeft | _heldRight;

    /// <summary>The modifiers held now on the right side only.</summary>
    public KeyModifiers HeldRightHand => _heldRight & ~_heldLeft;

    public KeyModifiers Modifiers { get; private set; }

    /// <summary>Which of <see cref="Modifiers"/> were held on the right side only; always within <see cref="Modifiers"/>.</summary>
    public KeyModifiers RightHand { get; private set; }

    public KeyCode Key { get; private set; }

    public bool HasCombination => Key != KeyCode.None;

    /// <summary>"Ctrl+Shift+T" once a key is pressed, "Ctrl+Shift+…" while only modifiers are held, null before anything.</summary>
    public string? LiveText => HasCombination
        ? HotkeyText.Format(Modifiers, Key, RightHand)
        : Held != KeyModifiers.None ? HotkeyText.Format(Held, KeyCode.None, HeldRightHand) + PendingSuffix : null;

    public void Reset()
    {
        _heldLeft = KeyModifiers.None;
        _heldRight = KeyModifiers.None;
        Modifiers = KeyModifiers.None;
        RightHand = KeyModifiers.None;
        Key = KeyCode.None;
    }

    public void Down(KeyCode key, KeyModifiers reported)
    {
        var modifier = HotkeyKeys.ModifierOf(key);
        if (modifier != KeyModifiers.None)
        {
            if (HotkeyKeys.IsRightHand(key))
            {
                _heldRight |= modifier;
            }
            else
            {
                _heldLeft |= modifier;
            }

            return;
        }

        if (key == KeyCode.None)
        {
            return;
        }

        Modifiers = (Held | reported) & HotkeyKeys.AllModifiers;
        RightHand = HeldRightHand & Modifiers;
        Key = key;
    }

    public void Up(KeyCode key)
    {
        var modifier = HotkeyKeys.ModifierOf(key);
        if (HotkeyKeys.IsRightHand(key))
        {
            _heldRight &= ~modifier;
        }
        else
        {
            _heldLeft &= ~modifier;
        }
    }
}
