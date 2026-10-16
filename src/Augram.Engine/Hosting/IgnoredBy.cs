using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Engine.Hosting;

/// <summary>
/// What made a press pass through untouched as an ignored press (<c>CaptureEvent.ButtonDown.IgnoreKeyHeld</c>), worked out by
/// the hook at the press and carried to the worker for its log line: a global ignore key (Options › General, F1) or an input
/// of the window's Exclusions › While holding entries (plan 0012). A value, so the hook allocates nothing for it; the worker
/// turns it into text (<see cref="Describe"/>).
/// </summary>
/// <param name="Kind">Which list, and which kind of input.</param>
/// <param name="Value">The <see cref="KeyModifiers"/> held, the <see cref="KeyCode"/> or the <see cref="MouseButton"/>, by <paramref name="Kind"/>.</param>
internal readonly record struct IgnoredBy(IgnoredByKind Kind, int Value)
{
    public static IgnoredBy None => default;

    public bool IsSet => Kind != IgnoredByKind.None;

    public static IgnoredBy IgnoreKey(KeyModifiers held) => new(IgnoredByKind.IgnoreKey, (int)held);

    public static IgnoredBy Modifier(KeyModifiers held) => new(IgnoredByKind.Modifier, (int)held);

    public static IgnoredBy Key(KeyCode key) => new(IgnoredByKind.Key, (int)key);

    public static IgnoredBy Button(MouseButton button) => new(IgnoredByKind.Button, (int)button);

    /// <summary>"ignore key: Alt", "while holding: Alt", "while holding: S", "while holding: Middle"; worker only (it allocates).</summary>
    public string Describe() => Kind switch
    {
        IgnoredByKind.IgnoreKey => $"ignore key: {HotkeyText.Format((KeyModifiers)Value, KeyCode.None)}",
        IgnoredByKind.Modifier => $"while holding: {HotkeyText.Format((KeyModifiers)Value, KeyCode.None)}",
        IgnoredByKind.Key => $"while holding: {new KeyOrButton.Key((KeyCode)Value).Describe(HotkeyText.Names)}",
        IgnoredByKind.Button => $"while holding: {new KeyOrButton.Button((MouseButton)Value).Describe(HotkeyText.Names)}",
        _ => string.Empty,
    };
}

/// <summary>The kinds of <see cref="IgnoredBy"/>: none, the global ignore keys, or a While holding entry's modifier, key or button.</summary>
internal enum IgnoredByKind
{
    None,
    IgnoreKey,
    Modifier,
    Key,
    Button,
}
