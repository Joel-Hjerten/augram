using System.Globalization;
using System.Text;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.TypeText;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// The state machine behind <see cref="SendKeysSyntax.Parse"/>: one pass over one string, one instance per string. The state is
/// the pending modifier prefix, the modifiers each open group holds, and the text run being built; a key flushes the run.
/// </summary>
internal sealed class SendKeysParser
{
    private readonly string _keys;
    private readonly TypeTextMethod _textMethod;
    private readonly List<IStep> _steps = [];
    private readonly List<string> _warnings = [];
    private readonly StringBuilder _text = new();
    private readonly Stack<KeyModifiers> _groups = new();
    private KeyModifiers _pending;

    public SendKeysParser(string keys, TypeTextMethod textMethod)
    {
        _keys = keys;
        _textMethod = textMethod;
    }

    private KeyModifiers Held => _groups.Count > 0 ? _groups.Peek() : KeyModifiers.None;

    public SendKeysResult Run()
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            switch (_keys[i])
            {
                case '^':
                    _pending |= KeyModifiers.Control;
                    break;
                case '+':
                    _pending |= KeyModifiers.Shift;
                    break;
                case '%':
                    _pending |= KeyModifiers.Alt;
                    break;
                case '@':
                    _pending |= KeyModifiers.Meta;
                    break;
                case '~':
                    AddKey(TakeModifiers(), KeyCode.Enter, 1);
                    break;
                case '(':
                    _groups.Push(TakeModifiers());
                    break;
                case ')':
                    CloseGroup();
                    break;
                case '{':
                    i = Brace(i);
                    break;
                default:
                    Character(_keys[i], 1);
                    break;
            }
        }

        DropPending("at the end");
        if (_groups.Count > 0)
        {
            _warnings.Add("'(' has no closing ')'; the group ends with the string.");
        }

        FlushText();
        return new SendKeysResult(_steps, _warnings);
    }

    /// <summary>The <c>{…}</c> starting at <paramref name="open"/>; returns the index of its closing brace. A brace right after the opening one is content (<c>{}}</c>).</summary>
    private int Brace(int open)
    {
        var close = open + 2 <= _keys.Length ? _keys.IndexOf('}', open + 2) : -1;
        if (close < 0)
        {
            _warnings.Add("'{' has no closing '}'; typed as text.");
            Character('{', 1);
            return open;
        }

        Token(_keys[(open + 1)..close]);
        return close;
    }

    private void Token(string token)
    {
        if (TryCommand(token, "DELAY", out var delay))
        {
            Delay(token, delay);
            return;
        }

        if (TryCommand(token, "VKEY", out var code))
        {
            VirtualKey(token, code);
            return;
        }

        var (name, count) = SplitRepeat(token);
        if (name.Length == 1)
        {
            Character(name[0], count);
        }
        else if (SendKeysNames.TryLiteral(name, out var literal))
        {
            Character(literal, count);
        }
        else if (SendKeysNames.TryKey(name, out var key))
        {
            AddKey(TakeModifiers(), key, count);
        }
        else
        {
            _pending = KeyModifiers.None;
            _warnings.Add($"{{{token}}} is not a key Augram knows; skipped.");
        }
    }

    private void Character(char c, int count)
    {
        var modifiers = TakeModifiers();
        if (modifiers == KeyModifiers.None)
        {
            _text.Append(c, count);
        }
        else if (AsciiKeyLayout.TryGetKey(c, out var key, out var shift))
        {
            AddKey(modifiers | (shift ? KeyModifiers.Shift : KeyModifiers.None), key, count);
        }
        else
        {
            _warnings.Add($"'{c}' has no key on a US layout to press with {Describe(modifiers)}; skipped.");
        }
    }

    private void AddKey(KeyModifiers modifiers, KeyCode key, int count)
    {
        if (count == 0)
        {
            return;
        }

        FlushText();
        IStep step = modifiers == KeyModifiers.None && MediaKind(key) is { } media
            ? new MediaKeyStep(media)
            : new HotkeyStep(modifiers, key);
        for (var n = 0; n < count; n++)
        {
            _steps.Add(step);
        }
    }

    private void Delay(string token, string argument)
    {
        DropPending($"before {{{token}}}");
        if (argument.StartsWith('='))
        {
            _warnings.Add($"{{{token}}} (a pause before every key) has no Augram equivalent; ignored.");
            return;
        }

        if (!int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
        {
            _warnings.Add($"{{{token}}} needs a whole number of milliseconds; skipped.");
            return;
        }

        FlushText();
        var clamped = Math.Min(milliseconds, DelayStepType.MaxMilliseconds);
        if (clamped != milliseconds)
        {
            _warnings.Add($"{{{token}}} clamped to {clamped} ms.");
        }

        _steps.Add(new DelayStep(clamped));
    }

    private void VirtualKey(string token, string argument)
    {
        var key = int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? HotkeyMapping.FromVirtualKey(code) : KeyCode.None;
        if (key == KeyCode.None)
        {
            _pending = KeyModifiers.None;
            _warnings.Add($"{{{token}}} names no key Augram knows (a decimal Windows virtual-key code); skipped.");
            return;
        }

        AddKey(TakeModifiers(), key, 1);
    }

    private void CloseGroup()
    {
        if (_groups.Count == 0)
        {
            _warnings.Add("')' closes no group; typed as text.");
            Character(')', 1);
            return;
        }

        DropPending("before ')'");
        _groups.Pop();
    }

    /// <summary>The modifiers the next key is pressed with: the open groups' plus the pending prefix, which this consumes.</summary>
    private KeyModifiers TakeModifiers()
    {
        var modifiers = Held | _pending;
        _pending = KeyModifiers.None;
        return modifiers;
    }

    private void DropPending(string where)
    {
        if (_pending != KeyModifiers.None)
        {
            _warnings.Add($"{Describe(_pending)} {where} applies to no key; dropped.");
            _pending = KeyModifiers.None;
        }
    }

    private (string Name, int Count) SplitRepeat(string token)
    {
        var space = token.LastIndexOf(' ');
        if (space <= 0 || !int.TryParse(token.AsSpan(space + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return (token, 1);
        }

        var name = token[..space].TrimEnd();
        if (name.Length == 0)
        {
            name = token[..space];
        }

        if (count > SendKeysSyntax.MaxRepeat)
        {
            _warnings.Add($"{{{token}}} repeats {SendKeysSyntax.MaxRepeat} times, not {count}.");
            count = SendKeysSyntax.MaxRepeat;
        }

        return (name, count);
    }

    private void FlushText()
    {
        if (_text.Length > 0)
        {
            _steps.Add(new TypeTextStep(_text.ToString(), _textMethod));
            _text.Clear();
        }
    }

    /// <summary><c>{DELAY 100}</c>, <c>{VKEY13}</c>: the name, then anything but a letter; the argument is the rest, trimmed.</summary>
    private static bool TryCommand(string token, string name, out string argument)
    {
        argument = string.Empty;
        if (!token.StartsWith(name, StringComparison.OrdinalIgnoreCase) || (token.Length > name.Length && char.IsLetter(token[name.Length])))
        {
            return false;
        }

        argument = token[name.Length..].Trim();
        return true;
    }

    private static MediaKeyKind? MediaKind(KeyCode key)
    {
        foreach (var kind in Enum.GetValues<MediaKeyKind>())
        {
            if (kind.ToKeyCode() == key)
            {
                return kind;
            }
        }

        return null;
    }

    private static string Describe(KeyModifiers modifiers) => HotkeyText.Format(modifiers, KeyCode.None, names: HostPlatform.Windows);
}
