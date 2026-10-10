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
/// Another program's release of a button through the whole engine with fakes (plan 0005 decision 10): Eyeris's loupe chord
/// swallows the real Right release and posts its own. The hook never suppresses that release; a background Right is no longer
/// held by the next trigger (Joel's log, 2026-10-10 19:37); a held-back Right ends with nothing replayed; a handed-back Right is
/// not released a second time. Every wait polls.
/// </summary>
public sealed class ReleasedElsewhereEngineTests
{
    private static readonly Trigger RightWheelUp = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right));

    [Fact]
    public void ARightLetThroughAndReleasedElsewhere_IsNotHeldByTheNextWheelTrigger()
    {
        var mapping = Mappings.Global(Mappings.Command("Volume up", Trigger.ForWheel(WheelDirection.Up), new WindowOpStep(WindowOperation.Minimize)));
        using var harness = new EngineHarness(Options(), mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("eyeris.exe");

        Assert.False(harness.Down(MouseButton.Right, 100, 100, 0), "Right is no anchor here: it passes");
        Assert.False(harness.Source.Deliver(RawInput.ButtonReleasedElsewhere(MouseButton.Right, 100, 100, 50)), "a release elsewhere is never suppressed");
        Assert.True(harness.Down(MouseButton.Middle, 100, 100, 500));
        Assert.True(harness.Wheel(WheelDirection.Up, 100, 100, 520));
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "Stroke + wheel up, with no Right held");
        Assert.True(harness.Up(MouseButton.Middle, 100, 100, 540));
    }

    [Fact]
    public void AHeldBackRightReleasedElsewhere_EndsWithNothingReplayed_AndItsRealReleaseIsSwallowed()
    {
        using var harness = ChromeWithRightWheel();

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 10));
        Assert.False(harness.Source.Deliver(RawInput.ButtonReleasedElsewhere(MouseButton.Right, 100, 100, 20)));
        harness.WaitForLog(LogSources.Capture, "Press released elsewhere");
        harness.WaitForState(CaptureState.Idle);
        Assert.True(harness.Up(MouseButton.Right, 100, 100, 30), "the real release still pairs the swallowed press");

        Assert.False(harness.Wheel(WheelDirection.Up, 100, 100, 40), "no press is held any more");
        Assert.Empty(harness.Simulator.Mouse);
        Assert.Empty(harness.WindowOperations.Calls);
    }

    [Fact]
    public void AHandedBackRightReleasedElsewhere_IsNotReleasedAgain()
    {
        using var harness = ChromeWithRightWheel();

        Assert.True(harness.Down(MouseButton.Right, 100, 100, 10));
        harness.Move(130, 100, 20);
        EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 2, "the hand-back");
        Assert.False(harness.Source.Deliver(RawInput.ButtonReleasedElsewhere(MouseButton.Right, 130, 100, 30)));
        harness.WaitForLog(LogSources.Capture, "Press released elsewhere");
        Assert.True(harness.Up(MouseButton.Right, 130, 100, 40));
        harness.WaitForState(CaptureState.Idle);

        Assert.Equal(["down Right@100,100", "move 130,100"], harness.Simulator.Mouse);
    }

    private static EngineHostOptions Options() => new(MouseButton.Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1));

    private static EngineHarness ChromeWithRightWheel()
    {
        var mapping = Mappings.Document([], Mappings.Group("Chrome", "chrome.exe", commands: Mappings.Command("Zoom in", RightWheelUp, new WindowOpStep(WindowOperation.Minimize))));
        var harness = new EngineHarness(Options(), mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("chrome.exe");
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.AnchorPlanUnderPointer.IsAnchor(MouseButton.Right), "Right to be an anchor over Chrome");
        return harness;
    }
}
