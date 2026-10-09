using System.Globalization;
using System.Text;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// A cursor over a StrokesPlus.net script (JavaScript run by ClearScript) that reads just enough of the language for
/// <see cref="RunProgramScript"/> and <see cref="ScriptTokens"/>: blanks and <c>//</c> or <c>/* */</c> comments, words, single characters, tokens, and string
/// literals in double or single quotes or backticks with JavaScript's escapes (<c>\\</c>, <c>\"</c>, <c>\n</c>,
/// <c>\xHH</c>, <c>\uHHHH</c>, <c>\u{…}</c>, a line continuation; an unknown escape is the character itself), plus
/// <c>String.raw</c> template literals, read raw. A template with <c>${…}</c> is not a plain string and is refused.
/// Every read returns false or null and leaves <see cref="Error"/> set instead of throwing.
/// </summary>
internal sealed class ScriptReader
{
    private readonly string _text;
    private int _position;

    public ScriptReader(string text)
    {
        _text = text;
    }

    /// <summary>Why the last string read failed; null while nothing failed.</summary>
    public string? Error { get; private set; }

    public bool AtEnd
    {
        get
        {
            SkipTrivia();
            return _position >= _text.Length;
        }
    }

    /// <summary>Skips blanks and comments; false when a block comment never ends.</summary>
    public bool SkipTrivia()
    {
        while (_position < _text.Length)
        {
            if (char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
            else if (Next("//"))
            {
                var end = _text.IndexOfAny(['\r', '\n'], _position);
                _position = end < 0 ? _text.Length : end;
            }
            else if (Next("/*"))
            {
                var end = _text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    _position = _text.Length;
                    return false;
                }

                _position = end + 2;
            }
            else
            {
                break;
            }
        }

        return true;
    }

    /// <summary>Consumes <paramref name="c"/> after trivia when it is next.</summary>
    public bool TryChar(char c)
    {
        SkipTrivia();
        if (_position < _text.Length && _text[_position] == c)
        {
            _position++;
            return true;
        }

        return false;
    }

    /// <summary>Consumes <paramref name="word"/> after trivia when it is next and not the start of a longer identifier.</summary>
    public bool TryWord(string word)
    {
        SkipTrivia();
        if (!Next(word))
        {
            return false;
        }

        var after = _position + word.Length;
        if (after < _text.Length && (char.IsLetterOrDigit(_text[after]) || _text[after] is '_' or '$'))
        {
            return false;
        }

        _position = after;
        return true;
    }

    /// <summary>A quoted or backtick string literal after trivia, its escapes applied; null (and <see cref="Error"/> when it started) otherwise.</summary>
    public string? TryString()
    {
        SkipTrivia();
        if (_position >= _text.Length || _text[_position] is not ('"' or '\'' or '`'))
        {
            return null;
        }

        var quote = _text[_position++];
        var value = new StringBuilder();
        while (_position < _text.Length)
        {
            var c = _text[_position++];
            if (c == quote)
            {
                return value.ToString();
            }

            if (quote == '`' && c == '$' && _position < _text.Length && _text[_position] == '{')
            {
                return Fail("a template literal with ${…} is not a plain string");
            }

            if (quote != '`' && c is '\r' or '\n')
            {
                return Fail("a string literal runs past the end of its line");
            }

            if (c != '\\')
            {
                value.Append(c);
            }
            else if (!Escape(value))
            {
                return null;
            }
        }

        return Fail("a string literal is not closed");
    }

    /// <summary>
    /// The next token after trivia, for <see cref="ScriptTokens"/>: a word (<c>sp</c>, <c>var</c>, <c>WM_MOUSEWHEEL</c>), a
    /// number with its letters and dots (<c>0x20A</c>, <c>1.5</c>), a string literal as <c>"value"</c> with its escapes
    /// applied (whatever its quotes), or one other character. Null at the end, or with <see cref="Error"/> set when a
    /// comment or a string is not closed.
    /// </summary>
    public string? TryToken()
    {
        if (!SkipTrivia())
        {
            return Fail("a comment is not closed");
        }

        if (_position >= _text.Length)
        {
            return null;
        }

        var first = _text[_position];
        if (first is '"' or '\'' or '`')
        {
            return TryString() is { } literal ? '"' + literal + '"' : null;
        }

        var start = _position++;
        if (char.IsLetterOrDigit(first) || first is '_' or '$')
        {
            while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] is '_' or '$' || (_text[_position] == '.' && char.IsDigit(first))))
            {
                _position++;
            }
        }

        return _text[start.._position];
    }

    /// <summary>The body of a template literal after <c>String.raw</c>, kept exactly as written; null when there is none.</summary>
    public string? TryRawTemplate()
    {
        SkipTrivia();
        if (_position >= _text.Length || _text[_position] != '`')
        {
            return Fail("String.raw must be followed by a template literal");
        }

        var start = ++_position;
        while (_position < _text.Length)
        {
            var c = _text[_position++];
            if (c == '`')
            {
                return _text[start..(_position - 1)];
            }

            if (c == '\\' && _position < _text.Length)
            {
                _position++;
            }
            else if (c == '$' && _position < _text.Length && _text[_position] == '{')
            {
                return Fail("a template literal with ${…} is not a plain string");
            }
        }

        return Fail("a template literal is not closed");
    }

    private bool Next(string text) => string.CompareOrdinal(_text, _position, text, 0, text.Length) == 0;

    private string? Fail(string error)
    {
        Error ??= error;
        return null;
    }

    /// <summary>One escape after its backslash, appended to <paramref name="value"/>.</summary>
    private bool Escape(StringBuilder value)
    {
        if (_position >= _text.Length)
        {
            Fail("a string literal is not closed");
            return false;
        }

        var c = _text[_position++];
        if (c == 'x')
        {
            return Hex(value, 2);
        }

        if (c == 'u')
        {
            return _position < _text.Length && _text[_position] == '{' ? CodePoint(value) : Hex(value, 4);
        }

        if (c is '\r' or '\n')
        {
            // A line continuation: the backslash and the line break vanish.
            if (c == '\r' && _position < _text.Length && _text[_position] == '\n')
            {
                _position++;
            }

            return true;
        }

        value.Append(c switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'b' => '\b',
            'f' => '\f',
            'v' => '\v',
            '0' => '\0',
            _ => c,
        });
        return true;
    }

    private bool Hex(StringBuilder value, int digits)
    {
        if (_position + digits > _text.Length
            || !int.TryParse(_text.AsSpan(_position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
        {
            Fail("a string literal has a bad \\x or \\u escape");
            return false;
        }

        _position += digits;
        value.Append((char)code);
        return true;
    }

    private bool CodePoint(StringBuilder value)
    {
        var end = _text.IndexOf('}', _position);
        if (end < 0
            || !int.TryParse(_text.AsSpan(_position + 1, end - _position - 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)
            || code > 0x10FFFF)
        {
            Fail("a string literal has a bad \\u{…} escape");
            return false;
        }

        _position = end + 1;
        if (code is >= 0xD800 and <= 0xDFFF)
        {
            value.Append((char)code);
        }
        else
        {
            value.Append(char.ConvertFromUtf32(code));
        }

        return true;
    }
}
