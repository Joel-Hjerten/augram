using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Execution;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Trigger combinations through the whole engine with fakes (F1, Joel 2026-10-09): an unbound click with keys reaches the app
/// with them held, a click trigger fires instead, anchors are held back per app (the watch's plan for the window under the
/// pointer), a dragged anchor is handed back with its injected down and up paired, a wheel tick after drawing fires no wheel
/// command, and a press with Win held taps Ctrl so Windows opens no Start menu. Every wait polls; acts are checked before log lines.
/// </summary>
public sealed class ChordEngineTests
{
    private static readonly Trigger RightWheelUp = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right));

    [Fact]
    public void AClickWithAnAfterKeyAndNothingBound_ReachesTheAppWithTheKeyHeld()
    {
        using var harness = new EngineHarness(mapping: Mappings.Global());

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 0));
        Assert.True(harness.Source.Deliver(RawInput.KeyDown(KeyCode.LeftShift, 10, KeyModifiers.Shift)));
        Assert.True(harness.Up(MouseButton.Right, 100, 100, 20));
        harness.WaitForLog(LogSources.Execution, "Click relayed");

        Assert.Equal([(MouseButton.Right, 100, 100)], harness.Simulator.Clicks);
        Assert.Equal(["press LeftShift", "release LeftShift"], harness.Simulator.Keys);
        Assert.True(harness.Source.Deliver(RawInput.KeyUp(KeyCode.LeftShift, 30)), "the swallowed key's release is swallowed too");
    }

    [Fact]
    public void AClickWithABeforeKeyAndNothingBound_IsRelayedPlain_TheKeyIsStillHeld()
    {
        using var harness = new EngineHarness(mapping: Mappings.Global());

        Assert.False(harness.Source.Deliver(RawInput.KeyDown(KeyCode.LeftShift, 0, KeyModifiers.Shift)));
        Assert.True(harness.Down(MouseButton.Right, 100, 100, 5, KeyModifiers.Shift));
        Assert.True(harness.Up(MouseButton.Right, 100, 100, 20));
        harness.WaitForLog(LogSources.Execution, "Click relayed");

        Assert.Equal([(MouseButton.Right, 100, 100)], harness.Simulator.Clicks);
        Assert.Empty(harness.Simulator.Keys);
        Assert.False(harness.Source.Deliver(RawInput.KeyUp(KeyCode.LeftShift, 30)));
    }

    [Fact]
    public void AClickTrigger_FiresItsCommand_AndTheClickGoesNowhere()
    {
        var mapping = Mappings.Global(Mappings.Command("Shift click", Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Shift)), new WindowOpStep(WindowOperation.Minimize)));
        using var harness = new EngineHarness(mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity();

        harness.Source.Deliver(RawInput.KeyDown(KeyCode.RightShift, 0, KeyModifiers.Shift));
        harness.Down(MouseButton.Right, 100, 100, 0, KeyModifiers.Shift);
        harness.Up(MouseButton.Right, 100, 100, 20);
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the click trigger's command");

        Assert.Empty(harness.Simulator.Clicks);
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    [Fact]
    public void AnAnchorIsHeldBackOverItsAppOnly_AndItsWheelFiresTheCommand()
    {
        using var harness = ChromeWithRightWheel();

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 10));
        Assert.True(harness.Wheel(WheelDirection.Up, 100, 100, 20));
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the Right + wheel up command");
        Assert.True(harness.Up(MouseButton.Right, 100, 100, 30));

        harness.Windows.Window = FakeWindowSystem.Identity("explorer.exe", handle: 0x9999, root: 0x9000);
        harness.Move(60, 60, 40);
        EngineHarness.WaitFor(() => !harness.Host.AnchorPlanUnderPointer.IsAnchor(MouseButton.Right), "Right to be no anchor over Explorer");
        Assert.False(harness.Down(MouseButton.Right, 60, 60, 50));
        Assert.False(harness.Wheel(WheelDirection.Up, 60, 60, 60));
        Assert.False(harness.Up(MouseButton.Right, 60, 60, 70));
        Assert.Empty(harness.Simulator.Mouse);
        Assert.Single(harness.WindowOperations.Calls);
    }

    [Fact]
    public void AnAnchorDragged_IsHandedBackAtOnce_ItsInjectedDownGetsItsUp()
    {
        using var harness = ChromeWithRightWheel();

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 10));
        harness.Move(105, 100, 20);
        harness.Move(112, 100, 30);
        harness.Move(180, 100, 40);
        EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 2, "the hand-back");
        Assert.True(harness.Up(MouseButton.Right, 180, 100, 50), "the physical release is swallowed; its injected release pairs the injected press");
        EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 3, "the injected release");

        // Handed back at the button drag distance (10 px by default), not the stroke's 30 px start distance.
        Assert.Equal(["down Right@100,100", "move 112,100", "up Right"], harness.Simulator.Mouse);
        Assert.Empty(harness.WindowOperations.Calls);
    }

    [Fact]
    public void AnAnchorClicked_IsReplayedAtRelease()
    {
        using var harness = ChromeWithRightWheel();

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 10));
        Assert.True(harness.Up(MouseButton.Right, 100, 100, 20));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.Equal([(MouseButton.Right, 100, 100)], harness.Simulator.Clicks);
    }

    [Fact]
    public void AWheelTickAfterDrawing_FiresNoWheelCommand()
    {
        var mapping = Mappings.Global(Mappings.Command("Volume", Trigger.ForWheel(WheelDirection.Up), new WindowOpStep(WindowOperation.Minimize)));
        using var harness = new EngineHarness(mapping: mapping);

        harness.Down(EngineHarness.StrokeButton, 100, 100, 0);
        harness.Move(130, 100, 10);
        harness.Move(170, 100, 20);
        Assert.True(harness.Wheel(WheelDirection.Up, 170, 100, 30));
        harness.Up(EngineHarness.StrokeButton, 170, 100, 40);
        harness.WaitForLog(LogSources.Capture, "Wheel after drawing; no command");

        Assert.Empty(harness.WindowOperations.Calls);
        Assert.DoesNotContain(harness.Events, e => e is EngineEvent.WheelTriggered);
    }

    [Fact]
    public void APressWithWinHeld_TapsCtrl_SoWindowsSeesNoLoneWin()
    {
        using var harness = new EngineHarness();

        Assert.False(harness.Source.Deliver(RawInput.KeyDown(KeyCode.LeftMeta, 0, KeyModifiers.Meta)));
        harness.Down(EngineHarness.StrokeButton, 100, 100, 5, KeyModifiers.Meta);
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 2, "the masking tap");

        Assert.Equal(["press LeftControl", "release LeftControl"], harness.Simulator.Keys);
    }

    /// <summary>
    /// A modifier mask the input library left stale (Win+L: Win's release happened on the secure desktop) holds no phantom
    /// key: Before keys are limited to keys this hook saw go down since its last reset (unlock and resume reset it).
    /// </summary>
    [Fact]
    public void AStaleModifierMask_IsNotABeforeKey_SoAPlainGestureStillFires()
    {
        using var harness = new EngineHarness(mapping: Mappings.Global());
        harness.Mapping = Mappings.Global(Mappings.Command("Minimize", Trigger.ForGesture(harness.Gestures[0].Id), new WindowOpStep(WindowOperation.Minimize)));
        harness.Windows.Window = FakeWindowSystem.Identity();

        harness.Down(EngineHarness.StrokeButton, 100, 100, 0, KeyModifiers.Meta);
        for (var i = 1; i <= 20; i++)
        {
            harness.Move(100 + (10 * i), 100, i * 10);
        }

        harness.Up(EngineHarness.StrokeButton, 300, 100, 220);
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the plain gesture's command");

        Assert.Empty(harness.Simulator.Keys);
    }

    /// <summary>Middle is the stroke button; Chrome has "Right + wheel up = minimize"; the pointer is over Chrome and the watch has said so.</summary>
    private static EngineHarness ChromeWithRightWheel()
    {
        var mapping = Mappings.Document([], Mappings.Group("Chrome", "chrome.exe", commands: Mappings.Command("Zoom in", RightWheelUp, new WindowOpStep(WindowOperation.Minimize))));
        var harness = new EngineHarness(
            new EngineHostOptions(MouseButton.Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1)),
            mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("chrome.exe");
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.AnchorPlanUnderPointer.IsAnchor(MouseButton.Right), "Right to be an anchor over Chrome");
        return harness;
    }
}
