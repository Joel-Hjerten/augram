using Augram.Core.Abstractions;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>What <see cref="HotkeyCaptureBox.Committed"/> carries: the kept combination, or no modifiers and <see cref="KeyCode.None"/> after Clear.</summary>
public sealed class HotkeyCommittedEventArgs(KeyModifiers modifiers, KeyCode key) : EventArgs
{
    public KeyModifiers Modifiers { get; } = modifiers;

    public KeyCode Key { get; } = key;
}
