using System.Collections.Frozen;
using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// Core <see cref="KeyCode"/> → macOS virtual key code (<c>kVK_ANSI_*</c> from HIToolbox's <c>Events.h</c>), pure and tested on
/// every OS, for the keys whose US name is the character they print: the letters, the top-row digits and the punctuation keys.
/// They are what <c>HotkeyText.KeyName</c> asks the layout about (plan 0011 decision 6); every other key has a name of its own
/// and is never translated, so it is not here. A virtual key names a physical position, the same under every layout; the
/// layout decides what it prints. The Engine's SharpHook map may not be used here (ADR-0002: Platform never references SharpHook).
/// </summary>
internal static class MacVirtualKeys
{
    private static readonly FrozenDictionary<KeyCode, ushort> Table = new Dictionary<KeyCode, ushort>
    {
        [KeyCode.A] = 0x00,
        [KeyCode.S] = 0x01,
        [KeyCode.D] = 0x02,
        [KeyCode.F] = 0x03,
        [KeyCode.H] = 0x04,
        [KeyCode.G] = 0x05,
        [KeyCode.Z] = 0x06,
        [KeyCode.X] = 0x07,
        [KeyCode.C] = 0x08,
        [KeyCode.V] = 0x09,
        [KeyCode.B] = 0x0B,
        [KeyCode.Q] = 0x0C,
        [KeyCode.W] = 0x0D,
        [KeyCode.E] = 0x0E,
        [KeyCode.R] = 0x0F,
        [KeyCode.Y] = 0x10,
        [KeyCode.T] = 0x11,
        [KeyCode.Digit1] = 0x12,
        [KeyCode.Digit2] = 0x13,
        [KeyCode.Digit3] = 0x14,
        [KeyCode.Digit4] = 0x15,
        [KeyCode.Digit6] = 0x16,
        [KeyCode.Digit5] = 0x17,
        [KeyCode.Equals] = 0x18,
        [KeyCode.Digit9] = 0x19,
        [KeyCode.Digit7] = 0x1A,
        [KeyCode.Minus] = 0x1B,
        [KeyCode.Digit8] = 0x1C,
        [KeyCode.Digit0] = 0x1D,
        [KeyCode.CloseBracket] = 0x1E,
        [KeyCode.O] = 0x1F,
        [KeyCode.U] = 0x20,
        [KeyCode.OpenBracket] = 0x21,
        [KeyCode.I] = 0x22,
        [KeyCode.P] = 0x23,
        [KeyCode.L] = 0x25,
        [KeyCode.J] = 0x26,
        [KeyCode.Quote] = 0x27,
        [KeyCode.K] = 0x28,
        [KeyCode.Semicolon] = 0x29,
        [KeyCode.Backslash] = 0x2A,
        [KeyCode.Comma] = 0x2B,
        [KeyCode.Slash] = 0x2C,
        [KeyCode.N] = 0x2D,
        [KeyCode.M] = 0x2E,
        [KeyCode.Period] = 0x2F,
        [KeyCode.BackQuote] = 0x32,
    }.ToFrozenDictionary();

    /// <summary>
    /// The keys with a name of their own that a remap button's held key may be (plan 0015: Space for Photoshop's pan), posted as
    /// CoreGraphics key events by <see cref="MacRemapButtonSimulator"/>: <c>kVK_Space</c>, <c>kVK_Return</c>, the F-keys, the
    /// arrows, the keypad. Never translated by a layout, so not in <see cref="TryMap"/>. Modifiers are not here (a held key is
    /// never one); media keys are not keyboard events on macOS.
    /// </summary>
    private static readonly FrozenDictionary<KeyCode, ushort> Named = new Dictionary<KeyCode, ushort>
    {
        [KeyCode.Space] = 0x31,
        [KeyCode.Tab] = 0x30,
        [KeyCode.Enter] = 0x24,
        [KeyCode.Escape] = 0x35,
        [KeyCode.Backspace] = 0x33,
        [KeyCode.Delete] = 0x75,
        [KeyCode.Home] = 0x73,
        [KeyCode.End] = 0x77,
        [KeyCode.PageUp] = 0x74,
        [KeyCode.PageDown] = 0x79,
        [KeyCode.Left] = 0x7B,
        [KeyCode.Right] = 0x7C,
        [KeyCode.Down] = 0x7D,
        [KeyCode.Up] = 0x7E,
        [KeyCode.F1] = 0x7A,
        [KeyCode.F2] = 0x78,
        [KeyCode.F3] = 0x63,
        [KeyCode.F4] = 0x76,
        [KeyCode.F5] = 0x60,
        [KeyCode.F6] = 0x61,
        [KeyCode.F7] = 0x62,
        [KeyCode.F8] = 0x64,
        [KeyCode.F9] = 0x65,
        [KeyCode.F10] = 0x6D,
        [KeyCode.F11] = 0x67,
        [KeyCode.F12] = 0x6F,
        [KeyCode.F13] = 0x69,
        [KeyCode.F14] = 0x6B,
        [KeyCode.F15] = 0x71,
        [KeyCode.F16] = 0x6A,
        [KeyCode.F17] = 0x40,
        [KeyCode.F18] = 0x4F,
        [KeyCode.F19] = 0x50,
        [KeyCode.F20] = 0x5A,
        [KeyCode.NumPad0] = 0x52,
        [KeyCode.NumPad1] = 0x53,
        [KeyCode.NumPad2] = 0x54,
        [KeyCode.NumPad3] = 0x55,
        [KeyCode.NumPad4] = 0x56,
        [KeyCode.NumPad5] = 0x57,
        [KeyCode.NumPad6] = 0x58,
        [KeyCode.NumPad7] = 0x59,
        [KeyCode.NumPad8] = 0x5B,
        [KeyCode.NumPad9] = 0x5C,
        [KeyCode.NumPadDecimal] = 0x41,
        [KeyCode.NumPadMultiply] = 0x43,
        [KeyCode.NumPadAdd] = 0x45,
        [KeyCode.NumPadDivide] = 0x4B,
        [KeyCode.NumPadSubtract] = 0x4E,
        [KeyCode.NumPadEnter] = 0x4C,
    }.ToFrozenDictionary();

    /// <summary>The virtual key for <paramref name="key"/>; false for every key that has a name of its own (F-keys, Esc, arrows, the keypad, media keys).</summary>
    public static bool TryMap(KeyCode key, out ushort virtualKey) => Table.TryGetValue(key, out virtualKey);

    /// <summary>The virtual key to post <paramref name="key"/> as: a <see cref="TryMap"/> key or one of the named keys above; false for the rest (modifiers, media keys, keys macOS has none for).</summary>
    public static bool TryMapForPosting(KeyCode key, out ushort virtualKey) => Table.TryGetValue(key, out virtualKey) || Named.TryGetValue(key, out virtualKey);
}
