using Augram.Core.Abstractions;
using Augram.Engine.Input;
using SharpHook.Data;
using SharpHook.Testing;
using Xunit;
using KeyCode = Augram.Core.Abstractions.KeyCode;
using MouseButton = Augram.Core.Capture.MouseButton;
using WheelAxis = SharpHook.Data.MouseWheelScrollDirection;
using WheelUnit = SharpHook.Data.MouseWheelScrollType;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// The tested pieces of the SharpHook simulator: which physical modifier keys a hotkey holds (the left keys as before;
/// the right-hand key, F5: RAlt, RCtrl, for a modifier in the right-hand set), what one wheel notch is on each
/// platform and how a scroll is sent, and a hold remap's button output (its modifiers around the press, no drag re-posting:
/// Windows as before plan 0002 step 3a), over SharpHook's own test hook.
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

    [Fact]
    public void ARemapButtonIsItsLeftModifiersAroundThePress_ItsReleaseAPlainRelease_AndNoDragIsReposted()
    {
        using var hook = new TestGlobalHook();
        var simulator = new SharpHookInputSimulator(hook);

        Assert.Equal(SimulationResult.Success, simulator.PressRemapButton(MouseButton.Middle, KeyModifiers.Shift | KeyModifiers.Control, 40, 50));
        Assert.Equal(SimulationResult.Success, simulator.ReleaseRemapButton(MouseButton.Middle));

        // Exactly what the worker posted itself before: Ctrl, Shift down, Middle down at the point, Shift, Ctrl up; Middle up.
        Assert.Equal(
            ["KeyPressed VcLeftControl", "KeyPressed VcLeftShift", "MousePressed Button3@40,50", "KeyReleased VcLeftShift", "KeyReleased VcLeftControl", "MouseReleased Button3"],
            Posted(hook));
        Assert.False(simulator.RepostsRemapDrags);
        Assert.Equal(SimulationResult.Unsupported, simulator.DragRemapButton(MouseButton.Middle, 45, 50, 5, 0));
        Assert.Equal(6, Posted(hook).Count());
    }

    [Fact]
    public void ARemapButtonWithoutModifiersIsAPlainPress()
    {
        using var hook = new TestGlobalHook();

        new SharpHookInputSimulator(hook).PressRemapButton(MouseButton.Left, KeyModifiers.None, 7, 8);

        Assert.Equal(["MousePressed Button1@7,8"], Posted(hook));
    }

    [Fact]
    public void AVerticalScrollAnnouncesItsNotchesSoTheHookDropsThem_AHorizontalOneDoesNot()
    {
        using var hook = new TestGlobalHook();
        var own = new OwnWheelInjections(() => 0);
        var simulator = new SharpHookInputSimulator(hook, own);

        simulator.Scroll(ScrollDirection.Down, 3, 0, 0);
        Assert.Equal(3, own.Pending);

        simulator.Scroll(ScrollDirection.Left, 2, 0, 0);
        Assert.Equal(3, own.Pending);
    }

    [Fact]
    public void EveryReleasePostedIsAnnouncedFirst_SoTheHookNeverTakesItForAnotherProgramsRelease()
    {
        using var hook = new TestGlobalHook();
        var own = new OwnButtonInjections(() => 0);
        var simulator = new SharpHookInputSimulator(hook, ownButtons: own);

        simulator.Click(MouseButton.Right, 10, 10);
        simulator.Press(MouseButton.Left, 10, 10);
        simulator.Release(MouseButton.Left);
        simulator.PressRemapButton(MouseButton.Middle, KeyModifiers.None, 10, 10);
        simulator.ReleaseRemapButton(MouseButton.Middle);

        Assert.Equal(1, own.Pending(MouseButton.Right));
        Assert.Equal(1, own.Pending(MouseButton.Left));
        Assert.Equal(1, own.Pending(MouseButton.Middle));
    }

    [Fact]
    public void AReleaseThatFailsToPostIsWithdrawn()
    {
        using var hook = new TestGlobalHook { SimulateMouseReleaseResult = UioHookResult.Failure };
        var own = new OwnButtonInjections(() => 0);

        var result = new SharpHookInputSimulator(hook, ownButtons: own).Release(MouseButton.Right);

        Assert.Equal(SimulationResult.Failed, result);
        Assert.Equal(0, own.Pending(MouseButton.Right));
    }

    /// <summary>What was posted; the test hook also reports the click libuiohook makes of a press and its release, which posts nothing.</summary>
    private static IEnumerable<string> Posted(TestGlobalHook hook) => hook.SimulatedEvents.Where(e => e.Type != EventType.MouseClicked).Select(Describe);

    private static string Describe(UioHookEvent e) => e.Type switch
    {
        EventType.KeyPressed or EventType.KeyReleased => $"{e.Type} {e.Keyboard.KeyCode}",
        EventType.MousePressed => $"{e.Type} {e.Mouse.Button}@{e.Mouse.X},{e.Mouse.Y}",
        _ => $"{e.Type} {e.Mouse.Button}",
    };
}
