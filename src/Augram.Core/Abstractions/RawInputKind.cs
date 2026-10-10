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

    /// <summary>
    /// Button, X, Y. A simulated release another program posted (not one of Augram's own): the OS now has the button up,
    /// whatever Augram saw of it (plan 0005 decision 10: a tool that swallows the real release and posts its own). Never
    /// suppressed.
    /// </summary>
    ButtonReleasedElsewhere,
}
