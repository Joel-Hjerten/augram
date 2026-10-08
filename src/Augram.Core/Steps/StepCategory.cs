namespace Augram.Core.Steps;

/// <summary>How the step picker groups types (F5a: "System built-ins · Hotkey · String · Command-prompt execution · Delay"). Declared by each type; the picker never lists kinds itself.</summary>
public enum StepCategory
{
    /// <summary>Window operations, volume, media playback: things the OS does.</summary>
    System,

    /// <summary>Display resolution, refresh rate and HDR (learnings 0002).</summary>
    Display,

    /// <summary>Hotkeys and key sequences.</summary>
    Keyboard,

    /// <summary>Typed strings.</summary>
    Text,

    /// <summary>Programs and command lines.</summary>
    Run,

    /// <summary>Delays and other flow control.</summary>
    Timing,

    /// <summary>
    /// Types the picker never offers: placeholders such as the imported step that only exist because
    /// a config or an import put them in a command. The picker lists every category but this one, so
    /// no screen needs to know a type's key to hide it.
    /// </summary>
    Other,
}
