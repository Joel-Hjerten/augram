using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// Which <see cref="KeyCode"/>s are modifier keys, which <see cref="KeyModifiers"/> flag each one holds
/// (left and right fold together, as the flags do) and which are the right-hand key (what
/// <see cref="HotkeyStep.RightHand"/> records).
/// </summary>
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

    /// <summary>True for the four right-hand modifier keys (RightControl, RightAlt, RightShift, RightMeta).</summary>
    public static bool IsRightHand(KeyCode key) => key is KeyCode.RightControl or KeyCode.RightAlt or KeyCode.RightShift or KeyCode.RightMeta;
}
