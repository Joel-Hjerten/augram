using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The pure half of <see cref="MacKeyboardLayouts"/>, tested on every OS: what Text Input Source Services' property values
/// become. A source is a keyboard layout when its type reads <c>kTISTypeKeyboardLayout</c> (input modes such as Japanese
/// Hiragana are not; they share a layout underneath). Its <see cref="KeyboardLayout"/> is its id (<c>com.apple.keylayout.Swedish-Pro</c>),
/// the text a Hotkey step's <c>layouts</c> is keyed by, and its localized name ("Swedish"); without an id there is no layout
/// (nothing could be stored for it), and without a name the id stands in. A translated string that is empty or blank is
/// no character.
/// </summary>
internal static class MacInputSources
{
    /// <summary>The <c>kTISTypeKeyboardLayout</c> constant's text: what a keyboard layout's <c>kTISPropertyInputSourceType</c> reads.</summary>
    public const string KeyboardLayoutType = "TISTypeKeyboardLayout";

    /// <summary>True for a source whose <c>kTISPropertyInputSourceType</c> is a keyboard layout.</summary>
    public static bool IsKeyboardLayout(string? type) => string.Equals(type, KeyboardLayoutType, StringComparison.Ordinal);

    /// <summary>The layout for a source's id and localized name; null without an id.</summary>
    public static KeyboardLayout? Layout(string? id, string? name)
        => string.IsNullOrEmpty(id) ? null : new KeyboardLayout(id, string.IsNullOrWhiteSpace(name) ? id : name);

    /// <summary>What <c>UCKeyTranslate</c>'s answer means as a character: null when it printed nothing or only whitespace (Space is not asked about, so this is "nothing").</summary>
    public static string? Character(string? translated) => string.IsNullOrWhiteSpace(translated) ? null : translated;
}
