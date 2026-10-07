using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns SP.net's keyboard methods into <see cref="HotkeyStep"/>s (plan 0001 §C1), pure and stateless so
/// the step reader and a later upgrade of saved placeholders share it. <c>SendHotKey</c> carries one
/// parameter <c>hotkey</c> whose value is an object, kept by <see cref="MethodParameterReader"/> as JSON
/// text: <c>{"LControl":true,"RControl":false,"LAlt":false,"RAlt":false,"LShift":true,"RShift":false,
/// "LWin":false,"RWin":false,"Key":84}</c>, where <c>Key</c> is a Windows virtual-key code. Left and right
/// fold into one flag (the model has no sides). <c>SendVKey</c> carries <c>virtualKey</c>, a bare code;
/// the media keys stay <c>MediaKeyStep</c>s (the reader maps them first) and every other mapped code is a
/// hotkey with no modifiers. Anything unreadable or unmapped gives null and stays a placeholder.
/// </summary>
public static class HotkeyMapping
{
    public const string HotkeyParameter = "hotkey";

    /// <summary>The <c>Key</c> member of the <c>hotkey</c> object.</summary>
    public const string KeyMember = "Key";

    // System.Windows.Forms.Keys modifier bits, in case a value carries them above the 16-bit key code.
    private const int KeysCodeMask = 0xFFFF;
    private const int KeysShift = 0x10000;
    private const int KeysControl = 0x20000;
    private const int KeysAlt = 0x40000;

    private static readonly FrozenDictionary<string, KeyModifiers> ModifierMembers =
        new Dictionary<string, KeyModifiers>(StringComparer.OrdinalIgnoreCase)
        {
            ["LControl"] = KeyModifiers.Control,
            ["RControl"] = KeyModifiers.Control,
            ["LAlt"] = KeyModifiers.Alt,
            ["RAlt"] = KeyModifiers.Alt,
            ["LShift"] = KeyModifiers.Shift,
            ["RShift"] = KeyModifiers.Shift,
            ["LWin"] = KeyModifiers.Meta,
            ["RWin"] = KeyModifiers.Meta,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Windows virtual-key codes (WinUser.h). The generic VK_SHIFT, VK_CONTROL and VK_MENU take the left key. OEM keys go by
    // their US-layout position; the code survives KeyCode → SharpHook → VK, so on another layout the same physical key comes out.
    private static readonly (int VirtualKey, KeyCode Key)[] NamedVirtualKeys =
    [
        (0x08, KeyCode.Backspace), (0x09, KeyCode.Tab), (0x0D, KeyCode.Enter), (0x10, KeyCode.LeftShift), (0x11, KeyCode.LeftControl),
        (0x12, KeyCode.LeftAlt), (0x13, KeyCode.Pause), (0x14, KeyCode.CapsLock), (0x1B, KeyCode.Escape), (0x20, KeyCode.Space),
        (0x21, KeyCode.PageUp), (0x22, KeyCode.PageDown), (0x23, KeyCode.End), (0x24, KeyCode.Home), (0x25, KeyCode.Left), (0x26, KeyCode.Up),
        (0x27, KeyCode.Right), (0x28, KeyCode.Down), (0x2C, KeyCode.PrintScreen), (0x2D, KeyCode.Insert), (0x2E, KeyCode.Delete),
        (0x5B, KeyCode.LeftMeta), (0x5C, KeyCode.RightMeta), (0x5D, KeyCode.ContextMenu), (0x6A, KeyCode.NumPadMultiply), (0x6B, KeyCode.NumPadAdd),
        (0x6D, KeyCode.NumPadSubtract), (0x6E, KeyCode.NumPadDecimal), (0x6F, KeyCode.NumPadDivide), (0x90, KeyCode.NumLock),
        (0x91, KeyCode.ScrollLock), (0xA0, KeyCode.LeftShift), (0xA1, KeyCode.RightShift), (0xA2, KeyCode.LeftControl), (0xA3, KeyCode.RightControl),
        (0xA4, KeyCode.LeftAlt), (0xA5, KeyCode.RightAlt), (0xA6, KeyCode.BrowserBack), (0xA7, KeyCode.BrowserForward),
        (0xA8, KeyCode.BrowserRefresh), (0xA9, KeyCode.BrowserStop), (0xAA, KeyCode.BrowserSearch), (0xAB, KeyCode.BrowserFavorites),
        (0xAC, KeyCode.BrowserHome), (0xAD, KeyCode.VolumeMute), (0xAE, KeyCode.VolumeDown), (0xAF, KeyCode.VolumeUp), (0xB0, KeyCode.MediaNext),
        (0xB1, KeyCode.MediaPrevious), (0xB2, KeyCode.MediaStop), (0xB3, KeyCode.MediaPlay), (0xB4, KeyCode.AppMail), (0xB7, KeyCode.AppCalculator),
        (0xBA, KeyCode.Semicolon), (0xBB, KeyCode.Equals), (0xBC, KeyCode.Comma), (0xBD, KeyCode.Minus), (0xBE, KeyCode.Period),
        (0xBF, KeyCode.Slash), (0xC0, KeyCode.BackQuote), (0xDB, KeyCode.OpenBracket), (0xDC, KeyCode.Backslash), (0xDD, KeyCode.CloseBracket),
        (0xDE, KeyCode.Quote),
    ];

    private static readonly FrozenDictionary<int, KeyCode> VirtualKeys = BuildVirtualKeys();

    /// <summary><c>SendHotKey</c>'s parameters as a step; null when the <c>hotkey</c> object is missing, unreadable, or its key has no <see cref="KeyCode"/>.</summary>
    public static HotkeyStep? FromSendHotKey(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!parameters.TryGetValue(HotkeyParameter, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? FromHotkeyObject(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary><c>SendVKey</c>'s parameters as a hotkey with no modifiers; null for a media key (those are <c>MediaKeyStep</c>s), a missing value, or an unmapped code.</summary>
    public static HotkeyStep? FromSendVKey(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!MethodParameterReader.TryInt32(parameters, StrokesPlusJson.Method.VirtualKeyParameter, out var code) || IsMediaVirtualKey(code))
        {
            return null;
        }

        var key = FromVirtualKey(code);
        return key == KeyCode.None ? null : new HotkeyStep(KeyModifiers.None, key);
    }

    /// <summary>
    /// A placeholder an earlier import stored for <c>SendHotKey</c> or <c>SendVKey</c>, as the real step; null for
    /// any other method or when the parameters do not map. Lets a saved config be upgraded without re-importing.
    /// </summary>
    public static HotkeyStep? TryUpgrade(ImportedStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.SourceMethod switch
        {
            StrokesPlusJson.Method.SendHotKey => FromSendHotKey(step.Parameters),
            StrokesPlusJson.Method.SendVKey => FromSendVKey(step.Parameters),
            _ => null,
        };
    }

    /// <summary>A Windows virtual-key code as a <see cref="KeyCode"/>; <see cref="KeyCode.None"/> when Augram has no name for it.</summary>
    public static KeyCode FromVirtualKey(int virtualKey) => VirtualKeys.GetValueOrDefault(virtualKey, KeyCode.None);

    /// <summary>VK_VOLUME_MUTE (173) through VK_MEDIA_PLAY_PAUSE (179): what the step reader turns into a <c>MediaKeyStep</c>.</summary>
    public static bool IsMediaVirtualKey(int virtualKey) => virtualKey is >= 0xAD and <= 0xB3;

    private static HotkeyStep? FromHotkeyObject(JsonElement hotkey)
    {
        var modifiers = KeyModifiers.None;
        int? code = null;
        foreach (var member in hotkey.EnumerateObject())
        {
            if (ModifierMembers.TryGetValue(member.Name, out var flag))
            {
                modifiers |= IsTrue(member.Value) ? flag : KeyModifiers.None;
            }
            else if (string.Equals(member.Name, KeyMember, StringComparison.OrdinalIgnoreCase))
            {
                code = Number(member.Value);
            }
        }

        if (code is not { } raw)
        {
            return null;
        }

        modifiers |= ((raw & KeysShift) != 0 ? KeyModifiers.Shift : KeyModifiers.None)
            | ((raw & KeysControl) != 0 ? KeyModifiers.Control : KeyModifiers.None)
            | ((raw & KeysAlt) != 0 ? KeyModifiers.Alt : KeyModifiers.None);
        var key = FromVirtualKey(raw & KeysCodeMask);
        return key == KeyCode.None ? null : new HotkeyStep(modifiers, key);
    }

    private static bool IsTrue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static int? Number(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt32(out var number) => number,
        JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    private static FrozenDictionary<int, KeyCode> BuildVirtualKeys()
    {
        var table = NamedVirtualKeys.ToDictionary(entry => entry.VirtualKey, entry => entry.Key);
        for (var i = 0; i < 26; i++)
        {
            table[0x41 + i] = KeyCode.A + i;
        }

        for (var i = 0; i < 10; i++)
        {
            table[0x30 + i] = KeyCode.Digit0 + i;
            table[0x60 + i] = KeyCode.NumPad0 + i;
        }

        for (var i = 0; i < 24; i++)
        {
            table[0x70 + i] = KeyCode.F1 + i;
        }

        return table.ToFrozenDictionary();
    }
}
