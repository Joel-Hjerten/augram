using Augram.Core.Abstractions;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// What <see cref="HotkeyCaptureBox.Committed"/> carries: the kept combination with the modifiers held on
/// the right side only (<see cref="RightHand"/>, within <see cref="Modifiers"/>), or nothing and
/// <see cref="KeyCode.None"/> after Clear.
/// </summary>
public sealed class HotkeyCommittedEventArgs(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand) : EventArgs
{
    public KeyModifiers Modifiers { get; } = modifiers;

    public KeyCode Key { get; } = key;

    public KeyModifiers RightHand { get; } = rightHand;
}
