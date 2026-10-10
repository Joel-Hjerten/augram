using System.Globalization;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// How a hotkey reads to a person: "Ctrl+Shift+T". The modifiers in a fixed order (Ctrl, Alt, Shift, Win on
/// Windows; Ctrl, Opt, Shift, Cmd on macOS, the order macOS menus use), a right-hand modifier as "RCtrl", "RAlt"…
/// in the same place ("Ctrl+RAlt+F9"), then the key's short name (letters and digits as themselves, "F5", "Esc",
/// "PgUp", "Left", "Num 4", "Volume Up"). The step summary, the App's capture field and dropdown, the log and the
/// importer's messages all use this, so a hotkey reads the same everywhere. The names are display only: Meta is
/// the Win key on Windows and the Command key on macOS, the same stored value under the name on that keyboard
/// (Joel, 2026-10-07: words, not ⌘⌥ symbols). Converting a Windows shortcut to its Mac counterpart (Ctrl+W → Cmd+W)
/// is a different matter, the F8 conversion.
/// </summary>
public static class HotkeyText
{
    public const string Separator = "+";

    private static readonly (KeyModifiers Flag, string Name, string RightName)[] WindowsModifiers =
    [
        (KeyModifiers.Control, "Ctrl", "RCtrl"),
        (KeyModifiers.Alt, "Alt", "RAlt"),
        (KeyModifiers.Shift, "Shift", "RShift"),
        (KeyModifiers.Meta, "Win", "RWin"),
    ];

    private static readonly (KeyModifiers Flag, string Name, string RightName)[] MacModifiers =
    [
        (KeyModifiers.Control, "Ctrl", "RCtrl"),
        (KeyModifiers.Alt, "Opt", "ROpt"),
        (KeyModifiers.Shift, "Shift", "RShift"),
        (KeyModifiers.Meta, "Cmd", "RCmd"),
    ];

    /// <summary>
    /// Whose key names a call without an explicit platform uses. Windows by default; the App sets it once at startup
    /// to the platform it runs on, so Core and its tests read the same on every machine.
    /// </summary>
    public static HostPlatform Names { get; set; } = HostPlatform.Windows;

    /// <summary>
    /// "Ctrl+Shift+T", "RCtrl+RShift+P"; the modifiers alone ("Ctrl+Shift") when <paramref name="key"/> is
    /// <see cref="KeyCode.None"/>; empty when both are empty. A <paramref name="rightHand"/> bit outside
    /// <paramref name="modifiers"/> prints nothing.
    /// </summary>
    public static string Format(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand = KeyModifiers.None, HostPlatform? names = null)
    {
        var platform = names ?? Names;
        var parts = new List<string>(5);
        foreach (var (flag, name, rightName) in platform == HostPlatform.MacOS ? MacModifiers : WindowsModifiers)
        {
            if ((modifiers & flag) != 0)
            {
                parts.Add((rightHand & flag) != 0 ? rightName : name);
            }
        }

        if (key != KeyCode.None)
        {
            parts.Add(KeyName(key, platform));
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// The four modifiers by name, in their order, as a list inside a sentence: "Ctrl, Alt, Shift and Win" on Windows,
    /// "Ctrl, Opt, Shift or Cmd" on macOS with "or" as <paramref name="conjunction"/>. The same names as <see cref="Format"/>.
    /// </summary>
    public static string ModifierList(string conjunction, HostPlatform? names = null)
    {
        var all = (names ?? Names) == HostPlatform.MacOS ? MacModifiers : WindowsModifiers;
        return $"{string.Join(", ", all[..^1].Select(modifier => modifier.Name))} {conjunction} {all[^1].Name}";
    }

    /// <summary>The short display name of one key; the enum name for anything without a nicer one.</summary>
    public static string KeyName(KeyCode key, HostPlatform? names = null) => (names ?? Names) == HostPlatform.MacOS
        ? key switch
        {
            KeyCode.LeftAlt => "Left Opt",
            KeyCode.RightAlt => "Right Opt",
            KeyCode.LeftMeta => "Left Cmd",
            KeyCode.RightMeta => "Right Cmd",
            _ => CommonKeyName(key),
        }
        : CommonKeyName(key);

    private static string CommonKeyName(KeyCode key) => key switch
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
