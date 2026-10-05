namespace Augram.Core.Abstractions;

/// <summary>
/// Platform-neutral key identity for steps and hotkeys (ADR-0002 §2). Values are stable (they end
/// up in config files); names deliberately match the input library's names without its prefix, so
/// the Engine maps by name and a test proves every member maps. Add members at the end of their
/// group with the next free value; never renumber.
/// </summary>
public enum KeyCode
{
    None = 0,

    // Letters
    A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8, I = 9, J = 10, K = 11, L = 12, M = 13,
    N = 14, O = 15, P = 16, Q = 17, R = 18, S = 19, T = 20, U = 21, V = 22, W = 23, X = 24, Y = 25, Z = 26,

    // Digits (top row)
    Digit0 = 30, Digit1 = 31, Digit2 = 32, Digit3 = 33, Digit4 = 34, Digit5 = 35, Digit6 = 36, Digit7 = 37, Digit8 = 38, Digit9 = 39,

    // Function keys
    F1 = 40, F2 = 41, F3 = 42, F4 = 43, F5 = 44, F6 = 45, F7 = 46, F8 = 47, F9 = 48, F10 = 49, F11 = 50, F12 = 51,
    F13 = 52, F14 = 53, F15 = 54, F16 = 55, F17 = 56, F18 = 57, F19 = 58, F20 = 59, F21 = 60, F22 = 61, F23 = 62, F24 = 63,

    // Editing and navigation
    Escape = 70, Tab = 71, Enter = 72, Space = 73, Backspace = 74, Insert = 75, Delete = 76,
    Home = 77, End = 78, PageUp = 79, PageDown = 80, Up = 81, Down = 82, Left = 83, Right = 84,
    PrintScreen = 85, ScrollLock = 86, Pause = 87, CapsLock = 88, NumLock = 89, ContextMenu = 90,

    // Modifiers, left and right
    LeftShift = 100, RightShift = 101, LeftControl = 102, RightControl = 103, LeftAlt = 104, RightAlt = 105,

    /// <summary>Left Win on Windows, left Command on macOS.</summary>
    LeftMeta = 106,

    /// <summary>Right Win on Windows, right Command on macOS.</summary>
    RightMeta = 107,

    // Punctuation (US layout names)
    BackQuote = 120, Minus = 121, Equals = 122, OpenBracket = 123, CloseBracket = 124, Backslash = 125,
    Semicolon = 126, Quote = 127, Comma = 128, Period = 129, Slash = 130,

    // Numeric keypad
    NumPad0 = 140, NumPad1 = 141, NumPad2 = 142, NumPad3 = 143, NumPad4 = 144, NumPad5 = 145, NumPad6 = 146,
    NumPad7 = 147, NumPad8 = 148, NumPad9 = 149, NumPadAdd = 150, NumPadSubtract = 151, NumPadMultiply = 152,
    NumPadDivide = 153, NumPadDecimal = 154, NumPadEnter = 155,

    // Media
    MediaPlay = 170, MediaStop = 171, MediaPrevious = 172, MediaNext = 173, VolumeMute = 174, VolumeDown = 175, VolumeUp = 176,

    // Browser and app launch keys
    BrowserBack = 190, BrowserForward = 191, BrowserRefresh = 192, BrowserStop = 193, BrowserSearch = 194,
    BrowserFavorites = 195, BrowserHome = 196, AppMail = 197, AppCalculator = 198,
}
