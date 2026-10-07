using Augram.Core.Abstractions;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// The one tested piece of the SharpHook simulator: which physical modifier keys a hotkey holds. The left
/// keys as before; the right-hand key (F5: RAlt, RCtrl) for a modifier in the right-hand set.
/// </summary>
public sealed class SharpHookInputSimulatorTests
{
    [Fact]
    public void PlainModifiersPressTheLeftKeysInCtrlAltShiftWinOrder()
    {
        Assert.Equal(
            [KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.LeftMeta],
            SharpHookInputSimulator.ModifierKeys(KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Control, KeyModifiers.None));
        Assert.Empty(SharpHookInputSimulator.ModifierKeys(KeyModifiers.None, KeyModifiers.None));
        Assert.Empty(SharpHookInputSimulator.ModifierKeys(KeyModifiers.None, KeyModifiers.Alt));
    }

    [Theory]
    [InlineData(KeyModifiers.Alt, KeyModifiers.Alt, new[] { KeyCode.RightAlt })]
    [InlineData(KeyModifiers.Control, KeyModifiers.Control, new[] { KeyCode.RightControl })]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Control | KeyModifiers.Shift, new[] { KeyCode.RightControl, KeyCode.RightShift })]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt, KeyModifiers.Alt, new[] { KeyCode.LeftControl, KeyCode.RightAlt })]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Meta, KeyModifiers.Meta, new[] { KeyCode.LeftShift, KeyCode.RightMeta })]
    [InlineData(KeyModifiers.Control, KeyModifiers.Alt | KeyModifiers.Shift, new[] { KeyCode.LeftControl })]
    public void RightHandModifiersPressTheRightKey_BitsOutsideTheModifiersPressNothing(KeyModifiers modifiers, KeyModifiers rightHand, KeyCode[] expected)
    {
        Assert.Equal(expected, SharpHookInputSimulator.ModifierKeys(modifiers, rightHand));
    }
}
