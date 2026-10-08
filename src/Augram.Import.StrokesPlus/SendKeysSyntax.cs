using Augram.Core.Abstractions;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.TypeText;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Parses the key syntax of StrokesPlus.net's <c>SendKeys</c> step (.NET <c>SendKeys</c> syntax plus the classic StrokesPlus
/// extras; requirements C1, plan 0001 §C1) into Augram steps. Pure and stateless; never throws for a malformed string, it
/// reports the part and goes on.
/// <list type="bullet">
/// <item>Plain characters → one <see cref="TypeTextStep"/> per run (consecutive characters merge), by the method asked for.</item>
/// <item><c>^</c> Ctrl, <c>+</c> Shift, <c>%</c> Alt, <c>@</c> Win (classic) apply to the next key, or to every key of a
/// parenthesised group (<c>^(ac)</c> is Ctrl+A, Ctrl+C); a character under a modifier becomes a <see cref="HotkeyStep"/>
/// with its US-layout key (<see cref="AsciiKeyLayout"/>), Shift added for a shifted character as .NET does
/// (<c>^A</c> is Ctrl+Shift+A).</item>
/// <item><c>~</c> and <c>{ENTER}</c>, <c>{TAB}</c>, <c>{F5}</c>… (<see cref="SendKeysNames"/>) → a <see cref="HotkeyStep"/>, or a
/// <see cref="MediaKeyStep"/> for a media key with no modifier; <c>{LEFT 3}</c> repeats it (at most <see cref="MaxRepeat"/>).</item>
/// <item><c>{+}</c>, <c>{^}</c>, <c>{%}</c>, <c>{~}</c>, <c>{(}</c>, <c>{)}</c>, <c>{{}</c>, <c>{}}</c>, <c>{@}</c>, any <c>{x}</c>
/// and the classic <c>{PLUS}</c>… names → that character; <c>{h 3}</c> repeats it.</item>
/// <item><c>{DELAY n}</c> → a <see cref="DelayStep"/> (clamped to its maximum); <c>{VKEY n}</c> → the key with Windows
/// virtual-key code n (decimal) through <see cref="HotkeyMapping.FromVirtualKey"/>.</item>
/// </list>
/// Anything else (an unknown name, the classic <c>{DELAY=n}</c>, a modifier that reaches no key, an unmatched bracket) is
/// reported in <see cref="SendKeysResult.Warnings"/>; unmatched brackets are typed as text.
/// </summary>
public static class SendKeysSyntax
{
    /// <summary>The most times a <c>{KEY n}</c> repeat count produces its step; a larger count is capped with a warning.</summary>
    public const int MaxRepeat = 100;

    public static SendKeysResult Parse(string keys, TypeTextMethod textMethod = TypeTextMethod.Unicode)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return new SendKeysParser(keys, textMethod).Run();
    }
}
