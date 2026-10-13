using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// One shortcut: a modifier set, the key, and which of the modifiers are the right-hand key. The shape a
/// <see cref="HotkeyStep"/>'s main shortcut and each of its layout shortcuts share (plan 0011), so a layout's
/// shortcut is a full hotkey of its own. <see cref="RightHand"/> bits outside <see cref="Modifiers"/> mean nothing
/// (the text and the executor ignore them; <see cref="Normalized"/> drops them), as on the step.
/// </summary>
public sealed record HotkeyBinding(KeyModifiers Modifiers, KeyCode Key, KeyModifiers RightHand = KeyModifiers.None)
{
    public const string UnsetSummary = "Hotkey (no key set)";

    public bool IsSet => Key != KeyCode.None;

    /// <summary>"Ctrl+Shift+T", "RAlt+F9"; <see cref="UnsetSummary"/> while no key is set.</summary>
    public string Summary => IsSet ? HotkeyText.Format(Modifiers, Key, RightHand) : UnsetSummary;

    /// <summary><see cref="Summary"/> in <paramref name="platform"/>'s key names ("Win+D" on Windows, "Cmd+D" on macOS).</summary>
    public string SummaryOn(HostPlatform platform) => IsSet ? HotkeyText.Format(Modifiers, Key, RightHand, platform) : UnsetSummary;

    /// <summary>True when <see cref="Normalized"/> would change nothing.</summary>
    public bool IsNormalized => (RightHand & ~Modifiers) == KeyModifiers.None;

    /// <summary>This binding with <see cref="RightHand"/> cut down to <see cref="Modifiers"/>; itself when it already is.</summary>
    public HotkeyBinding Normalized() => IsNormalized ? this : this with { RightHand = RightHand & Modifiers };
}
