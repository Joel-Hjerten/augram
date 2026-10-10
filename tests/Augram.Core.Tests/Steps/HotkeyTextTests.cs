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

    /// <summary>The modifiers as a list inside a sentence (hold remap refusals, plan 0002), in each platform's names and order.</summary>
    [Fact]
    public void ModifierListNamesTheFourModifiersAsThePlatformDoes()
    {
        Assert.Equal("Ctrl, Alt, Shift and Win", HotkeyText.ModifierList("and", HostPlatform.Windows));
        Assert.Equal("Ctrl, Opt, Shift or Cmd", HotkeyText.ModifierList("or", HostPlatform.MacOS));
    }

    [Theory]
    [InlineData(KeyModifiers.Control, KeyCode.Digit0, KeyModifiers.Control, "RCtrl+0")]
    [InlineData(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt, "RAlt+F9")]
    [InlineData(KeyModifiers.Shift, KeyCode.A, KeyModifiers.Shift, "RShift+A")]
    [InlineData(KeyModifiers.Meta, KeyCode.D, KeyModifiers.Meta, "RWin+D")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.P, KeyModifiers.Control | KeyModifiers.Shift, "RCtrl+RShift+P")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt, "Ctrl+RAlt+F9")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, KeyCode.Z, KeyModifiers.Alt | KeyModifiers.Meta, "Ctrl+RAlt+Shift+RWin+Z")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, KeyCode.Z, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, "RCtrl+RAlt+RShift+RWin+Z")]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Shift, KeyCode.None, KeyModifiers.Shift, "Alt+RShift")]
    [InlineData(KeyModifiers.Control, KeyCode.T, KeyModifiers.Alt, "Ctrl+T")]
    [InlineData(KeyModifiers.None, KeyCode.Tab, KeyModifiers.Control, "Tab")]
    public void RightHandModifiersReadAsRNamesInTheSameOrder_BitsOutsideTheModifiersPrintNothing(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand, string expected)
    {
        Assert.Equal(expected, HotkeyText.Format(modifiers, key, rightHand));
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

    [Theory]
    [InlineData(HostPlatform.Windows)]
    [InlineData(HostPlatform.MacOS)]
    public void EveryKeyHasADistinctNonEmptyName(HostPlatform platform)
    {
        var names = Enum.GetValues<KeyCode>().Select(key => HotkeyText.KeyName(key, platform)).ToList();

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

    [Theory]
    [InlineData(KeyModifiers.Meta, KeyCode.W, KeyModifiers.None, "Cmd+W")]
    [InlineData(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control, KeyCode.Z, KeyModifiers.None, "Ctrl+Opt+Shift+Cmd+Z")]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Meta, KeyCode.F9, KeyModifiers.Alt | KeyModifiers.Meta, "ROpt+RCmd+F9")]
    [InlineData(KeyModifiers.Control, KeyCode.Tab, KeyModifiers.None, "Ctrl+Tab")]
    public void Format_MacNames_SameKeysUnderTheMacKeyboardsNames(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand, string expected)
        => Assert.Equal(expected, HotkeyText.Format(modifiers, key, rightHand, HostPlatform.MacOS));

    [Theory]
    [InlineData(KeyCode.LeftMeta, "Left Cmd")]
    [InlineData(KeyCode.RightMeta, "Right Cmd")]
    [InlineData(KeyCode.LeftAlt, "Left Opt")]
    [InlineData(KeyCode.RightAlt, "Right Opt")]
    [InlineData(KeyCode.LeftControl, "Left Ctrl")]
    [InlineData(KeyCode.PageUp, "PgUp")]
    public void KeyName_MacNames(KeyCode key, string expected)
        => Assert.Equal(expected, HotkeyText.KeyName(key, HostPlatform.MacOS));

    [Fact]
    public void WithoutAPlatform_TheDefaultIsWindowsNames()
    {
        Assert.Equal(HostPlatform.Windows, HotkeyText.Names);
        Assert.Equal("Win+W", HotkeyText.Format(KeyModifiers.Meta, KeyCode.W));
    }

    [Fact]
    public void TheFourRightModifierKeysAreTheRightHandOnes()
    {
        Assert.Equal(
            [KeyCode.RightShift, KeyCode.RightControl, KeyCode.RightAlt, KeyCode.RightMeta],
            Enum.GetValues<KeyCode>().Where(HotkeyKeys.IsRightHand));
        Assert.All(Enum.GetValues<KeyCode>().Where(HotkeyKeys.IsRightHand), key => Assert.True(HotkeyKeys.IsModifier(key)));
    }

    [Fact]
    public void LeftKeysAreTheLeftHandKeyOfEachModifierInPressOrder()
    {
        Assert.Equal(
            [KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.LeftMeta],
            HotkeyKeys.LeftKeys(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control));
        Assert.Equal([KeyCode.LeftShift], HotkeyKeys.LeftKeys(KeyModifiers.Shift | (KeyModifiers)0x40));
        Assert.Empty(HotkeyKeys.LeftKeys(KeyModifiers.None));
    }
}
