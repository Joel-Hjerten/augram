using System.Collections.Frozen;
using Augram.Core.Abstractions;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// The <c>{NAME}</c> tokens <see cref="SendKeysSyntax"/> knows, matched ignoring case: the .NET SendKeys keywords
/// (<c>ENTER</c>, <c>ESC</c>, <c>PGUP</c>, <c>ADD</c>…, F1–F16, here F1–F24) and the classic StrokesPlus extras (<c>WIN</c>,
/// <c>APPS</c>, <c>NUMPAD0</c>…, <c>F_1</c>…, the browser and media keys), each as a <see cref="KeyCode"/>; and the classic
/// names that stand for a literal character (<c>PLUS</c>, <c>CARET</c>, <c>LBRACE</c>…). Names with no
/// <see cref="KeyCode"/> (<c>BREAK</c>, <c>CLEAR</c>, <c>HELP</c>, <c>SEPARATOR</c>, <c>SLEEP</c>) are left out, so they
/// are reported as unknown.
/// </summary>
internal static class SendKeysNames
{
    private static readonly FrozenDictionary<string, KeyCode> Keys = BuildKeys();

    private static readonly FrozenDictionary<string, char> Literals = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
    {
        ["PLUS"] = '+',
        ["CARET"] = '^',
        ["PERCENT"] = '%',
        ["TILDE"] = '~',
        ["AT"] = '@',
        ["LPAREN"] = '(',
        ["RPAREN"] = ')',
        ["LBRACE"] = '{',
        ["RBRACE"] = '}',
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool TryKey(string name, out KeyCode key) => Keys.TryGetValue(name, out key);

    public static bool TryLiteral(string name, out char literal) => Literals.TryGetValue(name, out literal);

    private static FrozenDictionary<string, KeyCode> BuildKeys()
    {
        var table = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            // .NET SendKeys keywords.
            ["ENTER"] = KeyCode.Enter,
            ["TAB"] = KeyCode.Tab,
            ["ESC"] = KeyCode.Escape,
            ["ESCAPE"] = KeyCode.Escape,
            ["HOME"] = KeyCode.Home,
            ["END"] = KeyCode.End,
            ["LEFT"] = KeyCode.Left,
            ["RIGHT"] = KeyCode.Right,
            ["UP"] = KeyCode.Up,
            ["DOWN"] = KeyCode.Down,
            ["PGUP"] = KeyCode.PageUp,
            ["PGDN"] = KeyCode.PageDown,
            ["NUMLOCK"] = KeyCode.NumLock,
            ["SCROLLLOCK"] = KeyCode.ScrollLock,
            ["PRTSC"] = KeyCode.PrintScreen,
            ["BACKSPACE"] = KeyCode.Backspace,
            ["BKSP"] = KeyCode.Backspace,
            ["BS"] = KeyCode.Backspace,
            ["CAPSLOCK"] = KeyCode.CapsLock,
            ["INS"] = KeyCode.Insert,
            ["INSERT"] = KeyCode.Insert,
            ["DEL"] = KeyCode.Delete,
            ["DELETE"] = KeyCode.Delete,
            ["MULTIPLY"] = KeyCode.NumPadMultiply,
            ["ADD"] = KeyCode.NumPadAdd,
            ["SUBTRACT"] = KeyCode.NumPadSubtract,
            ["DIVIDE"] = KeyCode.NumPadDivide,

            // Classic StrokesPlus names, and SPACE, which people write by habit.
            ["SPACE"] = KeyCode.Space,
            ["APPS"] = KeyCode.ContextMenu,
            ["DECIMAL"] = KeyCode.NumPadDecimal,
            ["SCROLL"] = KeyCode.ScrollLock,
            ["SNAPSHOT"] = KeyCode.PrintScreen,
            ["WIN"] = KeyCode.LeftMeta,
            ["LWIN"] = KeyCode.LeftMeta,
            ["RWIN"] = KeyCode.RightMeta,
            ["BROWSERBACK"] = KeyCode.BrowserBack,
            ["BROWSERFORWARD"] = KeyCode.BrowserForward,
            ["BROWSERREFRESH"] = KeyCode.BrowserRefresh,
            ["BROWSERSTOP"] = KeyCode.BrowserStop,
            ["BROWSERSEARCH"] = KeyCode.BrowserSearch,
            ["BROWSERFAVORITES"] = KeyCode.BrowserFavorites,
            ["BROWSERHOME"] = KeyCode.BrowserHome,
            ["MEDIANEXTTRACK"] = KeyCode.MediaNext,
            ["MEDIAPREVTRACK"] = KeyCode.MediaPrevious,
            ["MEDIAPLAYPAUSE"] = KeyCode.MediaPlay,
            ["MEDIASTOP"] = KeyCode.MediaStop,
            ["VOLUP"] = KeyCode.VolumeUp,
            ["VOLDOWN"] = KeyCode.VolumeDown,
            ["VOLMUTE"] = KeyCode.VolumeMute,
        };

        for (var i = 0; i < 24; i++)
        {
            table[$"F{i + 1}"] = KeyCode.F1 + i;
        }

        for (var i = 0; i < 9; i++)
        {
            table[$"F_{i + 1}"] = KeyCode.F1 + i;
        }

        for (var i = 0; i < 10; i++)
        {
            table[$"NUMPAD{i}"] = KeyCode.NumPad0 + i;
        }

        return table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
