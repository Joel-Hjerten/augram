#if DEBUG
using System.Collections.Frozen;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.App.DevGallery;

/// <summary>
/// The two keyboard layouts Joel switches between on the Mac, Swedish active, so the gallery's Hotkey form has per-layout
/// rows to show (plan 0011), each named by its own layout: the bracket, semicolon and quote keys print Å, Ö and Ä under
/// Swedish and [, ; and ' under ABC (Joel, 2026-10-13, first round); every other key answers nothing, so its US name stays.
/// The gallery's own (the App cannot reference test code); it mirrors <c>FakeKeyboardLayouts</c> in tests/Augram.TestSupport.
/// </summary>
internal sealed class GalleryKeyboardLayouts : IKeyboardLayouts
{
    public static GalleryKeyboardLayouts Instance { get; } = new();

    /// <summary>Chrome's Back: Cmd+Opt+8 under Swedish (the active layout, so the main shortcut the form derives) and Cmd+[ under ABC (where it reads "[", not "Å").</summary>
    public static HotkeyStep ChromeBack { get; } = new HotkeyStep(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8)
    {
        Layouts = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal)
        {
            [Instance.Installed[0].Id] = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8),
            [Instance.Installed[1].Id] = new(KeyModifiers.Meta, KeyCode.OpenBracket),
        }.ToFrozenDictionary(StringComparer.Ordinal),
    };

    private static readonly FrozenDictionary<string, FrozenDictionary<KeyCode, string>> Characters = new Dictionary<string, FrozenDictionary<KeyCode, string>>(StringComparer.Ordinal)
    {
        ["com.apple.keylayout.Swedish-Pro"] = new Dictionary<KeyCode, string> { [KeyCode.OpenBracket] = "å", [KeyCode.Semicolon] = "ö", [KeyCode.Quote] = "ä" }.ToFrozenDictionary(),
        ["com.apple.keylayout.ABC"] = new Dictionary<KeyCode, string> { [KeyCode.OpenBracket] = "[", [KeyCode.Semicolon] = ";", [KeyCode.Quote] = "'" }.ToFrozenDictionary(),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public KeyboardLayout? Active => Installed[0];

    public IReadOnlyList<KeyboardLayout> Installed { get; } = [new("com.apple.keylayout.Swedish-Pro", "Swedish"), new("com.apple.keylayout.ABC", "ABC")];

    public string? CharacterOf(KeyCode key, string? layoutId = null)
        => Characters.TryGetValue(layoutId ?? Installed[0].Id, out var characters) && characters.TryGetValue(key, out var character) ? character : null;
}
#endif
