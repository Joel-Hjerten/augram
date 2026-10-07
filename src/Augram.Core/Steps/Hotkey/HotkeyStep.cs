using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// Presses a modifier set and one key, then releases them in reverse order (F5: "modifier set + one
/// key"; a chord is a sequence of these steps). <see cref="KeyCode.None"/> is the explicit "no key set
/// yet" a new step starts with; running it skips. <see cref="RightHand"/> says which of
/// <see cref="Modifiers"/> are pressed with the right-hand key (RAlt, RCtrl: some apps treat them
/// differently, F5); a bit outside <see cref="Modifiers"/> means nothing, the text and the executor
/// ignore it, and <see cref="Normalized"/> drops it. The modifiers are platform-bound (F8): a step stores
/// what was authored, and the Ctrl ↔ Cmd conversion for macOS is a later slice.
/// </summary>
public sealed record HotkeyStep(KeyModifiers Modifiers, KeyCode Key, KeyModifiers RightHand = KeyModifiers.None) : IStep
{
    public const string UnsetSummary = "Hotkey (no key set)";

    /// <summary>No modifiers, no key: what the picker adds and what Clear returns to.</summary>
    public static HotkeyStep Unset { get; } = new(KeyModifiers.None, KeyCode.None);

    public IStepType Type => HotkeyStepType.Instance;

    public bool IsSet => Key != KeyCode.None;

    /// <summary>"Ctrl+Shift+T", "Alt+F4", "RCtrl+RShift+P", "Esc"; <see cref="UnsetSummary"/> while no key is set.</summary>
    public string Summary => IsSet ? HotkeyText.Format(Modifiers, Key, RightHand) : UnsetSummary;

    /// <summary>This step with <see cref="RightHand"/> cut down to <see cref="Modifiers"/>; itself when it already is.</summary>
    public HotkeyStep Normalized() => (RightHand & ~Modifiers) == KeyModifiers.None ? this : this with { RightHand = RightHand & Modifiers };
}
