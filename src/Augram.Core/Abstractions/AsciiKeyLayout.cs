namespace Augram.Core.Abstractions;

/// <summary>
/// Which key, with or without Shift, produces a printable ASCII character on a US layout. The one
/// table behind <see cref="IInputSimulator.TypeTextByKeys"/> for apps that read scan codes instead of
/// Unicode input (learnings 0001, B4): the Engine's simulator presses what it says, the Type text step
/// checks a text against it before typing by keys, and the StrokesPlus.net importer uses it to turn a
/// SendKeys character under a modifier (<c>^c</c>) into a hotkey. Characters outside ASCII, and control
/// characters other than newline and tab, have no key.
/// </summary>
public static class AsciiKeyLayout
{
    private const string Unshifted = "`1234567890-=[]\\;',./";
    private const string Shifted = "~!@#$%^&*()_+{}|:\"<>?";

    private static readonly KeyCode[] SymbolKeys =
    [
        KeyCode.BackQuote, KeyCode.Digit1, KeyCode.Digit2, KeyCode.Digit3, KeyCode.Digit4, KeyCode.Digit5,
        KeyCode.Digit6, KeyCode.Digit7, KeyCode.Digit8, KeyCode.Digit9, KeyCode.Digit0, KeyCode.Minus, KeyCode.Equals,
        KeyCode.OpenBracket, KeyCode.CloseBracket, KeyCode.Backslash, KeyCode.Semicolon, KeyCode.Quote,
        KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
    ];

    public static bool TryGetKey(char c, out KeyCode key, out bool shift)
    {
        shift = false;
        switch (c)
        {
            case >= 'a' and <= 'z':
                key = KeyCode.A + (c - 'a');
                return true;
            case >= 'A' and <= 'Z':
                key = KeyCode.A + (c - 'A');
                shift = true;
                return true;
            case ' ':
                key = KeyCode.Space;
                return true;
            case '\n':
                key = KeyCode.Enter;
                return true;
            case '\t':
                key = KeyCode.Tab;
                return true;
        }

        var index = Unshifted.IndexOf(c);
        if (index >= 0)
        {
            key = SymbolKeys[index];
            return true;
        }

        index = Shifted.IndexOf(c);
        if (index >= 0)
        {
            key = SymbolKeys[index];
            shift = true;
            return true;
        }

        key = KeyCode.None;
        return false;
    }

    /// <summary>The index of the first character in <paramref name="text"/> with no key, or -1 when every one has a key.</summary>
    public static int IndexOfUntypeable(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var i = 0; i < text.Length; i++)
        {
            if (!TryGetKey(text[i], out _, out _))
            {
                return i;
            }
        }

        return -1;
    }
}
