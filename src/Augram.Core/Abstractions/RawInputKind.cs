namespace Augram.Core.Abstractions;

/// <summary>What a <see cref="RawInput"/> is. A closed set; the fields that apply are listed on each member.</summary>
public enum RawInputKind
{
    /// <summary>Button, X, Y, Modifiers.</summary>
    ButtonDown,

    /// <summary>Button, X, Y, Modifiers.</summary>
    ButtonUp,

    /// <summary>X, Y.</summary>
    Move,

    /// <summary>Wheel, X, Y, Modifiers. Vertical ticks only; horizontal wheels are not a trigger in v1 (F1).</summary>
    Wheel,

    /// <summary>Key, Modifiers. Delivered for the hotkey-capture and ignore-key flows, never for text.</summary>
    KeyDown,

    /// <summary>Key, Modifiers.</summary>
    KeyUp,
}
