using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Execution;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Drag triggers through the whole engine with fakes (plan 0015): Joel's Photoshop pan, Global "Right + drag" remapped to Left with
/// Space held. Past the drag distance the worker presses Space, then Left at the start point, and drags it to where the pointer
/// is; Right's release releases Left, then Space. Where moves are re-posted (macOS) each move is swallowed and posted as a drag of
/// Left. A drag with ordinary steps runs them once; a drag no command takes (keys none names) goes back to the app as a hand-back.
/// A reset, the engine's stop and another program's release of Right release what the drag holds. Every wait polls.
/// </summary>
public sealed class DragTriggerEngineTests
{
    private const MouseButton Right = MouseButton.Right;

    private static readonly Trigger RightDrag = Trigger.ForDrag(new TriggerHold(HeldButtons.Right));

    private static readonly RemapOutput LeftWithSpace = new RemapOutput.Button(MouseButton.Left) { HeldKey = KeyCode.Space };

    [Fact]
    public void ADrag_PressesSpaceThenLeftAtTheStart_MovesPass_AndRightsReleaseReleasesLeftThenSpace()
    {
        using var harness = Pan();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(103, 100, 20), "a move always passes: inside the drag distance Right is still held back");
        Assert.False(harness.Move(130, 100, 30), "past it: the drag fires");
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 3, "Space, Left and the move to the pointer");
        harness.WaitForState(CaptureState.DragFiring);
        Assert.False(harness.Move(160, 110, 40), "Windows-style: the physical moves drag the posted Left as they are");
        Assert.True(harness.Up(Right, 160, 110, 50), "Right's release is swallowed: no context menu");
        harness.WaitForLog(LogSources.Execution, "Output released");

        Assert.Equal(["press Space", "down Left@100,100", "move 130,100", "up Left", "release Space"], harness.Simulator.All);
        Assert.Empty(harness.Simulator.Clicks);
        Assert.Equal("Pan", Property(harness.Log.Single(LogSources.Execution, "Output pressed"), "command"));
        Assert.Equal(KeyCode.Space, Property(harness.Log.Single(LogSources.Execution, "Output released"), "heldKey"));
        Assert.False(harness.Log.Has(LogSources.Capture, "Suppression decision mismatch"));
    }

    [Fact]
    public void AClick_IsStillReplayed_SoTheContextMenuOpens()
    {
        using var harness = Pan();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Up(Right, 102, 100, 60));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.Equal(["click Right@102,100"], harness.Simulator.All);
    }

    [Fact]
    public void Reposting_EveryMoveWhileLeftIsHeld_IsSwallowedAndPostedAsADragOfLeft_UntilRightsRelease()
    {
        // macOS (learnings 0005): physical right-drags do not move a posted Left.
        using var harness = Pan(new FakeInputSimulator(repostsRemapDrags: true));

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(115, 100, 20), "past the distance: the drag fires");
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 3, "Space, Left and the drag so far");
        Assert.True(harness.Move(140, 104, 30), "swallowed, re-posted as a drag of Left");
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 4, "the move re-posted");
        Assert.True(harness.Up(Right, 140, 104, 40));
        harness.WaitForLog(LogSources.Execution, "Output released");
        Assert.False(harness.Move(150, 104, 50), "nothing held any more: moves pass again");
        harness.WaitForState(CaptureState.Idle);

        Assert.Equal(["press Space", "down Left@100,100", "drag Left@115,100 by 15,0", "drag Left@140,104 by 25,4", "up Left", "release Space"], harness.Simulator.All);
        Assert.Equal(2, Property(harness.Log.Single(LogSources.Execution, "Output released"), "drags"));
    }

    [Fact]
    public void ADragWithOrdinarySteps_RunsThemOnce_AndHoldsNothing()
    {
        using var harness = Over(Mappings.Global(Mappings.Command("Minimize", RightDrag, new WindowOpStep(WindowOperation.Minimize))));

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(130, 100, 20));
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the drag's command");
        Assert.False(harness.Move(160, 100, 30));
        Assert.True(harness.Up(Right, 160, 100, 40));
        harness.WaitForState(CaptureState.Idle);
        Thread.Sleep(50);

        Assert.Single(harness.WindowOperations.Calls);
        Assert.Empty(harness.Simulator.All);
    }

    [Fact]
    public void ADragNoCommandTakes_GoesBackToTheApp_AsAHandBack()
    {
        // Only Shift + Right + drag is bound: a plain right-drag is the app's (a selection in a list), exactly as before plan 0015.
        var shifted = Trigger.ForDrag(new TriggerHold(HeldButtons.Right, KeyModifiers.Shift));
        using var harness = Over(Mappings.Global(Mappings.Command("Pan", shifted, new RemapStep(LeftWithSpace))));

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(130, 100, 20));
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 2, "Right handed back");
        Assert.True(harness.Up(Right, 140, 100, 30));
        harness.WaitForLog(LogSources.Capture, "Handed-back press released");

        Assert.Equal(["down Right@100,100", "move 130,100", "up Right"], harness.Simulator.All);
        Assert.False(harness.Log.Has(LogSources.Execution, "Output pressed"));
    }

    [Fact]
    public void AResetMidDrag_ReleasesLeftThenSpace()
    {
        using var harness = Pan();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(130, 100, 20));
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 3, "Space, Left and the move");
        harness.SystemEvents.Raise(SystemEventKind.SessionUnlocked);
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 5, "released at the reset");

        Assert.Equal(["up Left", "release Space"], harness.Simulator.All.Skip(3));
    }

    [Fact]
    public void TheEngineStoppingMidDrag_ReleasesLeftThenSpace()
    {
        var harness = Pan();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(130, 100, 20));
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 3, "Space, Left and the move");
        harness.Dispose();

        Assert.Equal(["press Space", "down Left@100,100", "move 130,100", "up Left", "release Space"], harness.Simulator.All);
    }

    [Fact]
    public void AnotherProgramsReleaseOfRight_ReleasesTheOutput()
    {
        using var harness = Pan();

        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.False(harness.Move(130, 100, 20));
        EngineHarness.WaitFor(() => harness.Simulator.All.Count == 3, "Space, Left and the move");
        Assert.False(harness.Source.Deliver(RawInput.ButtonReleasedElsewhere(Right, 130, 100, 30)));
        harness.WaitForLog(LogSources.Execution, "Output released");

        Assert.Equal(["press Space", "down Left@100,100", "move 130,100", "up Left", "release Space"], harness.Simulator.All);
    }

    private static object? Property(LogEvent e, string key) => e.Properties!.Single(p => p.Key == key).Value;

    private static EngineHarness Pan(FakeInputSimulator? simulator = null)
        => Over(Mappings.Global(Mappings.Command("Pan", RightDrag, new RemapStep(LeftWithSpace))), simulator);

    /// <param name="mapping">The commands, over Photoshop.</param>
    /// <param name="simulator">The default one does not re-post moves (as on Windows); one made with <c>repostsRemapDrags</c> does (as on macOS).</param>
    private static EngineHarness Over(MappingDocument mapping, FakeInputSimulator? simulator = null)
    {
        var harness = new EngineHarness(
            new EngineHostOptions(MouseButton.Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1)),
            mapping: MappingRules.ValidDocument(mapping),
            simulator: simulator);
        harness.Windows.Window = FakeWindowSystem.Identity("photoshop.exe");
        harness.Move(90, 90, 0);
        EngineHarness.WaitFor(() => harness.Host.AnchorPlanUnderPointer.IsAnchor(Right), "Right held back over Photoshop");
        return harness;
    }
}
