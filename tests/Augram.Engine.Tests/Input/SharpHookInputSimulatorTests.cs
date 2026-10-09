using Augram.Core.Abstractions;
using Augram.Engine.Input;
using SharpHook.Data;
using SharpHook.Testing;
using Xunit;
using KeyCode = Augram.Core.Abstractions.KeyCode;
using WheelAxis = SharpHook.Data.MouseWheelScrollDirection;
using WheelUnit = SharpHook.Data.MouseWheelScrollType;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// The tested pieces of the SharpHook simulator: which physical modifier keys a hotkey holds (the left keys as before;
/// the right-hand key, F5: RAlt, RCtrl, for a modifier in the right-hand set), and what one wheel notch is on each
/// platform and how a scroll is sent, over SharpHook's own test hook.
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

    [Theory]
    [InlineData(ScrollDirection.Up, false, 120, WheelAxis.Vertical, WheelUnit.UnitScroll)]
    [InlineData(ScrollDirection.Down, false, -120, WheelAxis.Vertical, WheelUnit.UnitScroll)]
    [InlineData(ScrollDirection.Left, false, 120, WheelAxis.Horizontal, WheelUnit.UnitScroll)]
    [InlineData(ScrollDirection.Right, false, -120, WheelAxis.Horizontal, WheelUnit.UnitScroll)]
    [InlineData(ScrollDirection.Up, true, 1, WheelAxis.Vertical, WheelUnit.BlockScroll)]
    [InlineData(ScrollDirection.Down, true, -1, WheelAxis.Vertical, WheelUnit.BlockScroll)]
    [InlineData(ScrollDirection.Left, true, 1, WheelAxis.Horizontal, WheelUnit.BlockScroll)]
    [InlineData(ScrollDirection.Right, true, -1, WheelAxis.Horizontal, WheelUnit.BlockScroll)]
    public void ANotchIsOneWheelClickOnEachPlatform_PositiveUpOrLeft(ScrollDirection direction, bool macOS, int rotation, WheelAxis axis, WheelUnit unit)
    {
        Assert.Equal(((short)rotation, axis, unit), SharpHookInputSimulator.Notch(direction, macOS));
    }

    [Fact]
    public void ScrollMovesToThePointThenSendsOneWheelEventPerNotch()
    {
        // SharpHook's test hook records what would be posted and posts nothing: no real wheel turns here.
        using var hook = new TestGlobalHook();
        var simulator = new SharpHookInputSimulator(hook);

        var result = simulator.Scroll(ScrollDirection.Down, 3, 40, 50);

        Assert.Equal(SimulationResult.Success, result);
        var events = hook.SimulatedEvents;
        Assert.Equal([EventType.MouseMoved, EventType.MouseWheel, EventType.MouseWheel, EventType.MouseWheel], events.Select(e => e.Type));
        Assert.Equal((40, 50), (events[0].Mouse.X, events[0].Mouse.Y));
        var (rotation, axis, _) = SharpHookInputSimulator.Notch(ScrollDirection.Down, OperatingSystem.IsMacOS());
        Assert.All(events.Skip(1), e => Assert.Equal((rotation, axis), (e.Wheel.Rotation, e.Wheel.Direction)));
    }

    [Fact]
    public void AFailedMoveSendsNoWheelEvent()
    {
        using var hook = new TestGlobalHook { SimulateMouseMovementResult = UioHookResult.Failure };

        var result = new SharpHookInputSimulator(hook).Scroll(ScrollDirection.Up, 2, 0, 0);

        Assert.Equal(SimulationResult.Failed, result);
        Assert.Empty(hook.SimulatedEvents);
    }
}
