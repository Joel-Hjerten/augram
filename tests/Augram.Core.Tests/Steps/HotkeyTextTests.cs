using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class HotkeyTextTests
{
    [Theory]
    [InlineData(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control, KeyCode.Z, "Ctrl+Alt+Shift+Win+Z")]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control, KeyCode.None, "Ctrl+Shift")]
    [InlineData(KeyModifiers.None, KeyCode.None, "")]
    [InlineData(KeyModifiers.None, KeyCode.Tab, "Tab")]
    [InlineData(KeyModifiers.Control, KeyCode.Equals, "Ctrl+=")]
    public void FormatPutsModifiersInWindowsOrderThenTheKey(KeyModifiers modifiers, KeyCode key, string expected)
    {
        Assert.Equal(expected, HotkeyText.Format(modifiers, key));
    }

    [Theory]
    [InlineData(KeyCode.A, "A")]
    [InlineData(KeyCode.Digit0, "0")]
    [InlineData(KeyCode.Digit9, "9")]
    [InlineData(KeyCode.F1, "F1")]
    [InlineData(KeyCode.F24, "F24")]
    [InlineData(KeyCode.Escape, "Esc")]
    [InlineData(KeyCode.Tab, "Tab")]
    [InlineData(KeyCode.PageUp, "PgUp")]
    [InlineData(KeyCode.PageDown, "PgDn")]
    [InlineData(KeyCode.Left, "Left")]
    [InlineData(KeyCode.PrintScreen, "PrtSc")]
    [InlineData(KeyCode.NumPad4, "Num 4")]
    [InlineData(KeyCode.NumPadEnter, "Num Enter")]
    [InlineData(KeyCode.LeftMeta, "Left Win")]
    [InlineData(KeyCode.Slash, "/")]
    [InlineData(KeyCode.Backslash, "\\")]
    [InlineData(KeyCode.VolumeUp, "Volume Up")]
    [InlineData(KeyCode.BrowserBack, "Browser Back")]
    [InlineData(KeyCode.None, "(no key)")]
    public void KeyNamesAreShortAndReadable(KeyCode key, string expected)
    {
        Assert.Equal(expected, HotkeyText.KeyName(key));
    }

    [Fact]
    public void EveryKeyHasADistinctNonEmptyName()
    {
        var names = Enum.GetValues<KeyCode>().Select(HotkeyText.KeyName).ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ModifierKeysFoldLeftAndRightIntoTheirFlag()
    {
        Assert.Equal(KeyModifiers.Control, HotkeyKeys.ModifierOf(KeyCode.LeftControl));
        Assert.Equal(KeyModifiers.Control, HotkeyKeys.ModifierOf(KeyCode.RightControl));
        Assert.Equal(KeyModifiers.Alt, HotkeyKeys.ModifierOf(KeyCode.RightAlt));
        Assert.Equal(KeyModifiers.Shift, HotkeyKeys.ModifierOf(KeyCode.LeftShift));
        Assert.Equal(KeyModifiers.Meta, HotkeyKeys.ModifierOf(KeyCode.RightMeta));
        Assert.Equal(KeyModifiers.None, HotkeyKeys.ModifierOf(KeyCode.T));
        Assert.True(HotkeyKeys.IsModifier(KeyCode.LeftMeta));
        Assert.False(HotkeyKeys.IsModifier(KeyCode.CapsLock));
        Assert.Equal(8, Enum.GetValues<KeyCode>().Count(HotkeyKeys.IsModifier));
    }
}
