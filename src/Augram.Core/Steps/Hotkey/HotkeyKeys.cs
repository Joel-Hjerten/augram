using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>Which <see cref="KeyCode"/>s are modifier keys and which <see cref="KeyModifiers"/> flag each one holds; left and right fold together, as the flags do.</summary>
public static class HotkeyKeys
{
    /// <summary>Every modifier flag, for masking a reported set down to the four a hotkey stores.</summary>
    public const KeyModifiers AllModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;

    /// <summary>The flag a modifier key holds; <see cref="KeyModifiers.None"/> for every other key.</summary>
    public static KeyModifiers ModifierOf(KeyCode key) => key switch
    {
        KeyCode.LeftControl or KeyCode.RightControl => KeyModifiers.Control,
        KeyCode.LeftAlt or KeyCode.RightAlt => KeyModifiers.Alt,
        KeyCode.LeftShift or KeyCode.RightShift => KeyModifiers.Shift,
        KeyCode.LeftMeta or KeyCode.RightMeta => KeyModifiers.Meta,
        _ => KeyModifiers.None,
    };

    public static bool IsModifier(KeyCode key) => ModifierOf(key) != KeyModifiers.None;
}
