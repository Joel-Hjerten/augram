using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.TypeText;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// SP.net <c>SendKeys</c> strings → steps. Every string is synthetic, in the shapes found in a real StrokesPlus.net config
/// (game console commands typed as text, Ctrl/Shift hotkeys with and without <c>{KEY}</c> tokens, punctuation-heavy text)
/// plus the rest of the .NET SendKeys syntax and the classic StrokesPlus extras.
/// </summary>
public sealed class SendKeysSyntaxTests
{
    private const KeyModifiers Ctrl = KeyModifiers.Control;
    private const KeyModifiers Shift = KeyModifiers.Shift;
    private const KeyModifiers Alt = KeyModifiers.Alt;
    private const KeyModifiers Win = KeyModifiers.Meta;
    private const KeyModifiers None = KeyModifiers.None;

    [Theory]
    [InlineData("hello world")]
    [InlineData("cmd 12.5")]
    [InlineData("`")]
    [InlineData(" -\"?hl=en\"")]
    [InlineData("a/b\\c;d'e,f.g[h]i=j-k")]
    [InlineData("line\nbreak")]
    public void PlainTextIsOneTypeTextStep(string keys)
    {
        var result = SendKeysSyntax.Parse(keys);

        Assert.Equal([T(keys)], result.Steps);
        Assert.True(result.IsClean);
    }

    [Fact]
    public void TextTakesTheMethodAskedFor()
    {
        Assert.Equal([new TypeTextStep("fov 70", TypeTextMethod.Keys)], SendKeysSyntax.Parse("fov 70", TypeTextMethod.Keys).Steps);
    }

    [Fact]
    public void AnEmptyStringGivesNothing()
    {
        var result = SendKeysSyntax.Parse(string.Empty);

        Assert.Empty(result.Steps);
        Assert.True(result.IsClean);
    }

    public static TheoryData<string, IStep> SingleHotkeys() => new()
    {
        { "^w", H(Ctrl, KeyCode.W) },
        { "^+t", H(Ctrl | Shift, KeyCode.T) },
        { "^+{TAB}", H(Ctrl | Shift, KeyCode.Tab) },
        { "^{TAB}", H(Ctrl, KeyCode.Tab) },
        { "{DELETE}", H(None, KeyCode.Delete) },
        { "{ESCAPE}", H(None, KeyCode.Escape) },
        { "^{ADD}", H(Ctrl, KeyCode.NumPadAdd) },
        { "^{SUBTRACT}", H(Ctrl, KeyCode.NumPadSubtract) },
        { "^{PGUP}", H(Ctrl, KeyCode.PageUp) },
        { "^{PGDN}", H(Ctrl, KeyCode.PageDown) },
        { "^{HOME}", H(Ctrl, KeyCode.Home) },
        { "^{END}", H(Ctrl, KeyCode.End) },
        { "{F5}", H(None, KeyCode.F5) },
        { "{F24}", H(None, KeyCode.F24) },
        { "%{F4}", H(Alt, KeyCode.F4) },
        { "@d", H(Win, KeyCode.D) },
        { "@{RIGHT}", H(Win, KeyCode.Right) },
        { "^%{DEL}", H(Ctrl | Alt, KeyCode.Delete) },
        { "{enter}", H(None, KeyCode.Enter) },
        { "{Esc}", H(None, KeyCode.Escape) },
        { "{BS}", H(None, KeyCode.Backspace) },
        { "{BKSP}", H(None, KeyCode.Backspace) },
        { "{INS}", H(None, KeyCode.Insert) },
        { "{UP}", H(None, KeyCode.Up) },
        { "{PRTSC}", H(None, KeyCode.PrintScreen) },
        { "~", H(None, KeyCode.Enter) },
        { "^~", H(Ctrl, KeyCode.Enter) },
        { "^-", H(Ctrl, KeyCode.Minus) },
        { "^0", H(Ctrl, KeyCode.Digit0) },
        { "^ ", H(Ctrl, KeyCode.Space) },
        // A shifted character under a modifier adds Shift, as .NET SendKeys does: ^A is Ctrl+Shift+A, ^! is Ctrl+Shift+1.
        { "^A", H(Ctrl | Shift, KeyCode.A) },
        { "^!", H(Ctrl | Shift, KeyCode.Digit1) },
        { "+a", H(Shift, KeyCode.A) },
        { "^{+}", H(Ctrl | Shift, KeyCode.Equals) },
        // Classic StrokesPlus names.
        { "{F_1}", H(None, KeyCode.F1) },
        { "{NUMPAD5}", H(None, KeyCode.NumPad5) },
        { "{WIN}", H(None, KeyCode.LeftMeta) },
        { "{RWIN}", H(None, KeyCode.RightMeta) },
        { "{APPS}", H(None, KeyCode.ContextMenu) },
        { "{BROWSERBACK}", H(None, KeyCode.BrowserBack) },
        { "{SPACE}", H(None, KeyCode.Space) },
        { "^{VOLUP}", H(Ctrl, KeyCode.VolumeUp) },
        // {VKEY n}: a decimal Windows virtual-key code, with or without the space.
        { "{VKEY 13}", H(None, KeyCode.Enter) },
        { "{VKEY13}", H(None, KeyCode.Enter) },
        { "^{VKEY 65}", H(Ctrl, KeyCode.A) },
        // A media key with no modifier is a Media key step.
        { "{VOLUP}", new MediaKeyStep(MediaKeyKind.VolumeUp) },
        { "{MEDIAPLAYPAUSE}", new MediaKeyStep(MediaKeyKind.PlayPause) },
        { "{VKEY 179}", new MediaKeyStep(MediaKeyKind.PlayPause) },
    };

    [Theory]
    [MemberData(nameof(SingleHotkeys))]
    public void ModifiersAndKeyTokensMakeOneKeyStep(string keys, IStep expected)
    {
        var result = SendKeysSyntax.Parse(keys);

        Assert.Equal([expected], result.Steps);
        Assert.True(result.IsClean, string.Join("; ", result.Warnings));
    }

    public static TheoryData<string, IStep[]> Sequences() => new()
    {
        { "abc~", [T("abc"), H(None, KeyCode.Enter)] },
        { "user{TAB}pass{ENTER}", [T("user"), H(None, KeyCode.Tab), T("pass"), H(None, KeyCode.Enter)] },
        { "^a^c", [H(Ctrl, KeyCode.A), H(Ctrl, KeyCode.C)] },
        { "^ab", [H(Ctrl, KeyCode.A), T("b")] },
        // Groups: the modifiers hold for every key inside; a group without modifiers is just text.
        { "^(ac)", [H(Ctrl, KeyCode.A), H(Ctrl, KeyCode.C)] },
        { "+(ab)c", [H(Shift, KeyCode.A), H(Shift, KeyCode.B), T("c")] },
        { "x(ab)y", [T("xaby")] },
        { "^(a+(b))", [H(Ctrl, KeyCode.A), H(Ctrl | Shift, KeyCode.B)] },
        { "%(f{DOWN})", [H(Alt, KeyCode.F), H(Alt, KeyCode.Down)] },
        // Repeat counts.
        { "{LEFT 3}", [H(None, KeyCode.Left), H(None, KeyCode.Left), H(None, KeyCode.Left)] },
        { "+{TAB 2}", [H(Shift, KeyCode.Tab), H(Shift, KeyCode.Tab)] },
        { "^{z 2}", [H(Ctrl, KeyCode.Z), H(Ctrl, KeyCode.Z)] },
        { "a{h 3}b", [T("ahhhb")] },
        { "{} 2}", [T("}}")] },
        { "a{LEFT 0}x", [T("ax")] },
        // Escaped literals and the classic literal names join the text around them.
        { "{+}{^}{%}{~}{(}{)}{{}{}}{@}{[}{]}", [T("+^%~(){}@[]")] },
        { "1{+}1=2", [T("1+1=2")] },
        { "{PLUS}{CARET}{PERCENT}{TILDE}{AT}{LPAREN}{RPAREN}{LBRACE}{RBRACE}", [T("+^%~@(){}")] },
        { "{PLUS 2}", [T("++")] },
        { "a{ }b", [T("a b")] },
        // Delays split the text.
        { "{DELAY 250}", [new DelayStep(250)] },
        { "ab{DELAY 100}cd", [T("ab"), new DelayStep(100), T("cd")] },
        { "`{delay 20}fov 70~", [T("`"), new DelayStep(20), T("fov 70"), H(None, KeyCode.Enter)] },
    };

    [Theory]
    [MemberData(nameof(Sequences))]
    public void SequencesKeepTheirOrderAndMergeConsecutiveText(string keys, IStep[] expected)
    {
        var result = SendKeysSyntax.Parse(keys);

        Assert.Equal(expected, result.Steps);
        Assert.True(result.IsClean, string.Join("; ", result.Warnings));
    }

    public static TheoryData<string, IStep[], string> Problems() => new()
    {
        { "{NOPE}", [], "{NOPE} is not a key Augram knows; skipped." },
        { "^{NOPE}a", [T("a")], "{NOPE} is not a key Augram knows; skipped." },
        { "{BREAK}", [], "{BREAK} is not a key Augram knows; skipped." },
        { "{BEEP 440 100}", [], "{BEEP 440 100} is not a key Augram knows; skipped." },
        { "ab^", [T("ab")], "Ctrl at the end applies to no key; dropped." },
        { "(^)", [], "Ctrl before ')' applies to no key; dropped." },
        { "a)", [T("a)")], "')' closes no group; typed as text." },
        { "^(ab", [H(Ctrl, KeyCode.A), H(Ctrl, KeyCode.B)], "'(' has no closing ')'; the group ends with the string." },
        { "{abc", [T("{abc")], "'{' has no closing '}'; typed as text." },
        { "x{", [T("x{")], "'{' has no closing '}'; typed as text." },
        { "^é", [], "'é' has no key on a US layout to press with Ctrl; skipped." },
        { "{DELAY 999999}", [new DelayStep(DelayStepType.MaxMilliseconds)], "{DELAY 999999} clamped to 60000 ms." },
        { "a{DELAY=50}b", [T("ab")], "{DELAY=50} (a pause before every key) has no Augram equivalent; ignored." },
        { "{DELAY soon}", [], "{DELAY soon} needs a whole number of milliseconds; skipped." },
        { "^{DELAY 10}", [new DelayStep(10)], "Ctrl before {DELAY 10} applies to no key; dropped." },
        { "{VKEY 255}", [], "{VKEY 255} names no key Augram knows (a decimal Windows virtual-key code); skipped." },
        { "{VKEY 0x41}", [], "{VKEY 0x41} names no key Augram knows (a decimal Windows virtual-key code); skipped." },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void WhatCannotBeMappedIsReportedAndTheRestStillParses(string keys, IStep[] expected, string warning)
    {
        var result = SendKeysSyntax.Parse(keys);

        Assert.Equal(expected, result.Steps);
        Assert.Equal([warning], result.Warnings);
        Assert.False(result.IsClean);
    }

    [Fact]
    public void ARepeatCountIsCapped()
    {
        var result = SendKeysSyntax.Parse("{LEFT 500}");

        Assert.Equal(SendKeysSyntax.MaxRepeat, result.Steps.Count);
        Assert.All(result.Steps, step => Assert.Equal(H(None, KeyCode.Left), step));
        Assert.Equal(["{LEFT 500} repeats 100 times, not 500."], result.Warnings);
    }

    private static TypeTextStep T(string text) => new(text, TypeTextMethod.Unicode);

    private static HotkeyStep H(KeyModifiers modifiers, KeyCode key) => new(modifiers, key);
}
