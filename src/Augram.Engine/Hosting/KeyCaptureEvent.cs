using Augram.Core.Abstractions;

namespace Augram.Engine.Hosting;

/// <summary>
/// One report to an <see cref="EngineHost.CaptureKeys"/> callback (F5 hotkey capture): a key event seen
/// while the capture was armed, with the modifier mask the hook reported, or the engine's own release
/// with its <see cref="Reason"/>. <see cref="Key"/> is <see cref="KeyCode.None"/> for a key Core has no
/// name for and for <see cref="KeyCaptureEventKind.Released"/>.
/// </summary>
public readonly record struct KeyCaptureEvent(KeyCaptureEventKind Kind, KeyCode Key, KeyModifiers Modifiers, string? Reason = null)
{
    /// <summary>No key event arrived within the idle timeout given to <see cref="EngineHost.CaptureKeys"/>.</summary>
    public const string IdleTimeoutReason = "idle timeout";

    /// <summary>Another caller armed a capture; only one is ever armed.</summary>
    public const string ReplacedReason = "replaced";

    /// <summary>The hook was reinstalled, or the session resumed or was unlocked; the reason text follows in brackets.</summary>
    public const string HookResetReason = "hook reset";

    public const string EngineStoppedReason = "engine stopped";

    /// <summary>The caller disposed its capture; logged, never reported back to it.</summary>
    public const string DisposedReason = "released by caller";

    public bool IsDown => Kind == KeyCaptureEventKind.KeyDown;

    public static KeyCaptureEvent From(in RawInput input)
        => new(input.Kind == RawInputKind.KeyUp ? KeyCaptureEventKind.KeyUp : KeyCaptureEventKind.KeyDown, input.Key, input.Modifiers);

    public static KeyCaptureEvent KeyDown(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) => new(KeyCaptureEventKind.KeyDown, key, modifiers);

    public static KeyCaptureEvent KeyUp(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) => new(KeyCaptureEventKind.KeyUp, key, modifiers);

    public static KeyCaptureEvent Released(string reason) => new(KeyCaptureEventKind.Released, KeyCode.None, KeyModifiers.None, reason);
}
