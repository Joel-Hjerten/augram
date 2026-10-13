#if DEBUG
using System.Collections.Frozen;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.App.DevGallery;

/// <summary>
/// The two keyboard layouts Joel switches between on the Mac, Swedish active, so the gallery's Hotkey form has per-layout
/// rows to show (plan 0011); it names no characters. The gallery's own (the App cannot reference test code); it mirrors
/// <c>FakeKeyboardLayouts</c> in tests/Augram.TestSupport.
/// </summary>
internal sealed class GalleryKeyboardLayouts : IKeyboardLayouts
{
    public static GalleryKeyboardLayouts Instance { get; } = new();

    /// <summary>Chrome's Back: Cmd+[ as the main shortcut, Cmd+Opt+8 under Swedish, nothing yet under ABC.</summary>
    public static HotkeyStep ChromeBack { get; } = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket)
    {
        Layouts = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal)
        {
            [Instance.Installed[0].Id] = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8),
        }.ToFrozenDictionary(StringComparer.Ordinal),
    };

    public KeyboardLayout? Active => Installed[0];

    public IReadOnlyList<KeyboardLayout> Installed { get; } = [new("com.apple.keylayout.Swedish-Pro", "Swedish"), new("com.apple.keylayout.ABC", "ABC")];

    public string? CharacterOf(KeyCode key) => null;
}
#endif
