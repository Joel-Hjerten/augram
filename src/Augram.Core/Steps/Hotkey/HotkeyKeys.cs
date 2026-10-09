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

    private static readonly KeyCode[] LeftKeysInPressOrder = [KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.LeftMeta];

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

    /// <summary>The left-hand key of each modifier in <paramref name="modifiers"/>, in press order (Ctrl, Alt, Shift, Meta): what a step holds down for a modifier set.</summary>
    public static IReadOnlyList<KeyCode> LeftKeys(KeyModifiers modifiers)
        => [.. LeftKeysInPressOrder.Where(key => (modifiers & ModifierOf(key)) != 0)];
}
