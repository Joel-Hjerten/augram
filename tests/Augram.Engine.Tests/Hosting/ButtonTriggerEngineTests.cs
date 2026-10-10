using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Execution;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Button triggers through the whole engine with fakes (plan 0005): Joel's magnifier, Global "Right + Left" holding Win+Shift+X.
/// The key is held exactly while both buttons are down (a tap of Left is a short press, so Eyeris can latch its loupe), pressed
/// again with Left, released at the anchor's release, a reset or the engine's stop; Right's own release replays nothing; a button
/// trigger with ordinary steps runs them once at the press. Every wait polls.
/// </summary>
public sealed class ButtonTriggerEngineTests
{
    private const MouseButton Right = MouseButton.Right;
    private const MouseButton Left = MouseButton.Left;

    private static readonly Trigger RightLeft = Trigger.ForButton(Left, new TriggerHold(HeldButtons.Right));

    private static readonly string[] Pressed = ["press LeftShift", "press LeftMeta", "press X"];

    private static readonly string[] Released = ["release X", "release LeftMeta", "release LeftShift"];

    [Fact]
    public void TheKeyIsHeldWhileBothAreDown_ReleasedWithLeft_PressedAgain_AndRightsReleaseReplaysNothing()
    {
        using var harness = Magnifier();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Down(Left, 100, 100, 50));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 3, "Win+Shift+X pressed");
        Assert.Equal(Pressed, harness.Simulator.Keys);
        Assert.True(harness.Up(Left, 100, 100, 120));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 6, "Win+Shift+X released");

        Assert.True(harness.Down(Left, 100, 100, 400));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 9, "pressed again");
        Assert.True(harness.Up(Right, 100, 100, 600), "Right's release is swallowed: no context menu");
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 12, "released with Right");
        Assert.True(harness.Up(Left, 100, 100, 650));
        harness.WaitForState(CaptureState.Idle);

        Assert.Equal([.. Pressed, .. Released, .. Pressed, .. Released], harness.Simulator.Keys);
        Assert.Empty(harness.Simulator.Mouse);
        harness.WaitForLog(LogSources.Execution, "Button output held");
    }

    [Fact]
    public void WhileTheKeyIsHeld_MovesAndWheelTicksPass_AndNothingIsHandedBack()
    {
        using var harness = Magnifier();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Down(Left, 100, 100, 50));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 3, "Win+Shift+X pressed");
        harness.WaitForState(CaptureState.ButtonFiring);
        Assert.False(harness.Move(400, 300, 100));
        Assert.False(harness.Wheel(WheelDirection.Up, 400, 300, 2000), "a loupe may use the wheel");
        Thread.Sleep(50);

        Assert.Empty(harness.Simulator.Mouse);
        Assert.Equal(CaptureState.ButtonFiring, harness.Host.State);
        Assert.True(harness.Up(Left, 400, 300, 2100));
        Assert.True(harness.Up(Right, 400, 300, 2200));
    }

    [Fact]
    public void AResetMidChord_ReleasesTheKey()
    {
        using var harness = Magnifier();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Down(Left, 100, 100, 50));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 3, "Win+Shift+X pressed");
        harness.SystemEvents.Raise(SystemEventKind.SessionUnlocked);
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 6, "released at the reset");

        Assert.Equal([.. Pressed, .. Released], harness.Simulator.Keys);
    }

    [Fact]
    public void TheEngineStoppingMidChord_ReleasesTheKey()
    {
        var harness = Magnifier();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Down(Left, 100, 100, 50));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 3, "Win+Shift+X pressed");
        harness.Dispose();

        Assert.Equal([.. Pressed, .. Released], harness.Simulator.Keys);
    }

    [Fact]
    public void ABackRocker_RunsItsStepsOnceAtThePress()
    {
        var mapping = Mappings.Global(Mappings.Command("Back", RightLeft, new WindowOpStep(WindowOperation.Minimize)));
        using var harness = Over(mapping);

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Down(Left, 100, 100, 50));
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the rocker's command");
        Assert.True(harness.Up(Left, 100, 100, 100));
        Assert.True(harness.Up(Right, 100, 100, 150));
        harness.WaitForState(CaptureState.Idle);

        Assert.Single(harness.WindowOperations.Calls);
        Assert.Empty(harness.Simulator.Keys);
        Assert.Empty(harness.Simulator.Mouse);
    }

    [Fact]
    public void APlainRightClick_IsStillReplayed()
    {
        using var harness = Magnifier();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Up(Right, 100, 100, 60));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.Equal([(Right, 100, 100)], harness.Simulator.Clicks);
        Assert.Empty(harness.Simulator.Keys);
    }

    [Fact]
    public void OverAnExcludedApp_AnAlsoInChordWorks_TheStrokeButtonPasses_AndAnotherExcludedAppHoldsNothingBack()
    {
        var blender = new IgnoredApp(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
        var game = new IgnoredApp(GroupId.New(), "Plague", IsActive: true, new AppMatcher { WindowsProcessNames = ["plague.exe"] }, DisableEntirely: false);
        var magnifier = Mappings.Command("Magnifier", RightLeft, new RemapStep(new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta))) with { AlsoIn = [blender.Id] };
        var mapping = MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal with { Commands = [magnifier] }], [blender, game]));
        using var harness = new EngineHarness(
            new EngineHostOptions(MouseButton.Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1)),
            mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("blender.exe");
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer?.Id == blender.Id && harness.Host.AnchorPlanUnderPointer.Fires(Right, Left), "Right + Left to fire over Blender");

        Assert.False(harness.Down(MouseButton.Middle, 100, 100, 10), "Blender keeps its Middle");
        Assert.False(harness.Up(MouseButton.Middle, 100, 100, 20));
        Assert.True(harness.Down(Right, 100, 100, 30));
        Assert.True(harness.Down(Left, 100, 100, 60));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 3, "Win+Shift+X pressed over Blender");
        Assert.True(harness.Up(Left, 100, 100, 100));
        Assert.True(harness.Up(Right, 100, 100, 120));
        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 6, "Win+Shift+X released");

        harness.Windows.Window = FakeWindowSystem.Identity("plague.exe", handle: 0x9999, root: 0x9000);
        harness.Move(60, 60, 200);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer?.Id == game.Id && harness.Host.AnchorPlanUnderPointer.IsEmpty, "nothing held back over the game");
        Assert.False(harness.Down(Right, 60, 60, 210), "the game gets its right-click at once");
        Assert.False(harness.Down(Left, 60, 60, 220));
        Assert.False(harness.Up(Left, 60, 60, 230));
        Assert.False(harness.Up(Right, 60, 60, 240));
        Assert.Equal(6, harness.Simulator.Keys.Count);
        Assert.Empty(harness.Simulator.Mouse);
    }

    private static EngineHarness Magnifier()
    {
        var output = new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta);
        return Over(Mappings.Global(Mappings.Command("Magnifier", RightLeft, new RemapStep(output))));
    }

    private static EngineHarness Over(MappingDocument mapping)
    {
        var harness = new EngineHarness(
            new EngineHostOptions(MouseButton.Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1)),
            mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("chrome.exe");
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.AnchorPlanUnderPointer.Fires(Right, Left), "Right + Left to fire over Chrome");
        return harness;
    }
}
