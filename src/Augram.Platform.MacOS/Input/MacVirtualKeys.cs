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

    /// <summary>The virtual key for <paramref name="key"/>; false for every key that has a name of its own (F-keys, Esc, arrows, the keypad, media keys).</summary>
    public static bool TryMap(KeyCode key, out ushort virtualKey) => Table.TryGetValue(key, out virtualKey);
}
