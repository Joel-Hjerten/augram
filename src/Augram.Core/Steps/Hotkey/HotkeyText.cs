using System.Globalization;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// How a hotkey reads to a person: "Ctrl+Shift+T". Windows names (Ctrl, Alt, Shift, Win) in that
/// order, a right-hand modifier as "RCtrl", "RAlt", "RShift", "RWin" in the same place ("Ctrl+RAlt+F9"),
/// then the key's short name (letters and digits as themselves, "F5", "Esc", "PgUp", "Left", "Num 4",
/// "Volume Up"). The step summary, the App's capture field and dropdown, and the importer's messages all
/// use this, so a hotkey reads the same everywhere. macOS names (⌘, ⌥) arrive with the F8 conversion slice.
/// </summary>
public static class HotkeyText
{
    public const string Separator = "+";

    private static readonly (KeyModifiers Flag, string Name, string RightName)[] ModifierOrder =
    [
        (KeyModifiers.Control, "Ctrl", "RCtrl"),
        (KeyModifiers.Alt, "Alt", "RAlt"),
        (KeyModifiers.Shift, "Shift", "RShift"),
        (KeyModifiers.Meta, "Win", "RWin"),
    ];

    /// <summary>
    /// "Ctrl+Shift+T", "RCtrl+RShift+P"; the modifiers alone ("Ctrl+Shift") when <paramref name="key"/> is
    /// <see cref="KeyCode.None"/>; empty when both are empty. A <paramref name="rightHand"/> bit outside
    /// <paramref name="modifiers"/> prints nothing.
    /// </summary>
    public static string Format(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None)
    {
        var parts = new List<string>(5);
        foreach (var (flag, name, rightName) in ModifierOrder)
        {
            if ((modifiers & flag) != 0)
            {
                parts.Add((rightHand & flag) != 0 ? rightName : name);
            }
        }

        if (key != KeyCode.None)
        {
            parts.Add(KeyName(key));
        }

        return string.Join(Separator, parts);
    }

    /// <summary>The short display name of one key; the enum name for anything without a nicer one.</summary>
    public static string KeyName(KeyCode key) => key switch
    {
        >= KeyCode.Digit0 and <= KeyCode.Digit9 => Digit(key - KeyCode.Digit0),
        >= KeyCode.NumPad0 and <= KeyCode.NumPad9 => "Num " + Digit(key - KeyCode.NumPad0),
        KeyCode.None => "(no key)",
        KeyCode.Escape => "Esc",
        KeyCode.Insert => "Ins",
        KeyCode.Delete => "Del",
        KeyCode.PageUp => "PgUp",
        KeyCode.PageDown => "PgDn",
        KeyCode.PrintScreen => "PrtSc",
        KeyCode.ScrollLock => "ScrLk",
        KeyCode.ContextMenu => "Menu",
        KeyCode.LeftShift => "Left Shift",
        KeyCode.RightShift => "Right Shift",
        KeyCode.LeftControl => "Left Ctrl",
        KeyCode.RightControl => "Right Ctrl",
        KeyCode.LeftAlt => "Left Alt",
        KeyCode.RightAlt => "Right Alt",
        KeyCode.LeftMeta => "Left Win",
        KeyCode.RightMeta => "Right Win",
        KeyCode.BackQuote => "`",
        KeyCode.Minus => "-",
        KeyCode.Equals => "=",
        KeyCode.OpenBracket => "[",
        KeyCode.CloseBracket => "]",
        KeyCode.Backslash => "\\",
        KeyCode.Semicolon => ";",
        KeyCode.Quote => "'",
        KeyCode.Comma => ",",
        KeyCode.Period => ".",
        KeyCode.Slash => "/",
        KeyCode.NumPadAdd => "Num +",
        KeyCode.NumPadSubtract => "Num -",
        KeyCode.NumPadMultiply => "Num *",
        KeyCode.NumPadDivide => "Num /",
        KeyCode.NumPadDecimal => "Num .",
        KeyCode.NumPadEnter => "Num Enter",
        KeyCode.MediaPlay => "Play/Pause",
        KeyCode.MediaStop => "Media Stop",
        KeyCode.MediaPrevious => "Previous Track",
        KeyCode.MediaNext => "Next Track",
        KeyCode.VolumeMute => "Mute",
        KeyCode.VolumeDown => "Volume Down",
        KeyCode.VolumeUp => "Volume Up",
        KeyCode.BrowserBack => "Browser Back",
        KeyCode.BrowserForward => "Browser Forward",
        KeyCode.BrowserRefresh => "Browser Refresh",
        KeyCode.BrowserStop => "Browser Stop",
        KeyCode.BrowserSearch => "Browser Search",
        KeyCode.BrowserFavorites => "Browser Favorites",
        KeyCode.BrowserHome => "Browser Home",
        KeyCode.AppMail => "Mail",
        KeyCode.AppCalculator => "Calculator",
        _ => key.ToString(),
    };

    private static string Digit(int value) => value.ToString(CultureInfo.InvariantCulture);
}
