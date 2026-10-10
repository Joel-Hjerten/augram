using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using Augram.Engine.Input;
using Augram.Engine.Tests.Fakes;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Joel's Blender scenarios through the whole engine with fakes (F9, plan 0002 step 3): the hook decides from the foreground's
/// plan the watch published, the worker plays the outputs in order, the executor runs a Steps command. The <c>Reposting_</c>
/// cases use a simulator that asks for drags to be re-posted, as macOS's does (plan 0002 step 3a); the rest are Windows-style.
/// Every wait polls; acts are checked before log lines, and the last log line of a scenario says the worker got that far.
/// </summary>
public sealed class HoldRemapEngineTests
{
    private const MouseButton Left = MouseButton.Left;
    private const MouseButton Right = MouseButton.Right;
    private const MouseButton Middle = MouseButton.Middle;
    private static readonly WindowIdentity Notepad = FakeWindowSystem.Identity("notepad.exe", handle: 0x10);

    [Fact]
    public void SpaceAndLeft_Orbit_MiddleIsInjected_AndLeftNeverReachesTheApp()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 20));
        Assert.False(harness.Move(150, 100, 30), "Windows-style: the physical drag moves the posted Middle as it is");
        Assert.True(harness.Up(Left, 150, 100, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 60));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Middle@100,100", "up Middle"], harness.Simulator.All);
        Assert.Empty(harness.Trail.Calls);
        Assert.Empty(harness.Events);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void RollingLeft_LeftAndRight_Right_ReleasesTheOldOutputBeforePressingTheNew()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        Assert.True(harness.Down(Right, 110, 100, 20));
        Assert.True(harness.Up(Left, 120, 100, 30));
        Assert.True(harness.Up(Right, 130, 100, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 50));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(
            [
                "down Middle@100,100",
                "up Middle", "down Control+Middle@110,100",
                "up Middle", "down Shift+Middle@120,100",
                "up Middle",
            ],
            harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void Reposting_SpaceAndRight_ShiftAroundTheDown_ThenEveryMoveAsADrag_ThenUp()
    {
        // macOS (learnings 0005): a posted Middle moves only with drags posted for it, so the physical moves are swallowed.
        using var harness = Blender(reposts: true);

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.False(harness.Move(95, 100, 5), "nothing held yet: the move passes");
        Assert.True(harness.Down(Right, 100, 100, 10));
        Assert.True(harness.Move(110, 100, 20), "swallowed, re-posted as a drag of the output");
        Assert.True(harness.Move(125, 106, 30));
        Assert.True(harness.Up(Right, 125, 106, 40));
        Assert.False(harness.Move(130, 106, 50), "nothing held any more: moves pass again");
        Assert.True(harness.KeyUp(KeyCode.Space, 60));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Shift+Middle@100,100", "drag Middle@110,100 by 10,0", "drag Middle@125,106 by 15,6", "up Middle"], harness.Simulator.All);
        Assert.Equal(2, Property(harness.Log.Single(LogSources.Hold, "Output released"), "drags"));
        AssertNoMismatch(harness);
    }

    [Fact]
    public void Reposting_RollingLeft_LeftAndRight_Right_DragsUnderEachOutputInTurn()
    {
        using var harness = Blender(reposts: true);

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        Assert.True(harness.Move(105, 100, 15));
        Assert.True(harness.Down(Right, 110, 100, 20));
        Assert.True(harness.Move(112, 104, 25));
        Assert.True(harness.Up(Left, 120, 104, 30));
        Assert.True(harness.Move(121, 110, 35));
        Assert.True(harness.Up(Right, 130, 110, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 50));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        // Each delta is from the last physical position the worker saw: the move before, or the button event that turned the output.
        Assert.Equal(
            [
                "down Middle@100,100", "drag Middle@105,100 by 5,0",
                "up Middle", "down Control+Middle@110,100", "drag Middle@112,104 by 2,4",
                "up Middle", "down Shift+Middle@120,104", "drag Middle@121,110 by 1,6",
                "up Middle",
            ],
            harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void Reposting_ASetWithNoCommand_PassesMoves_UntilAButtonOutputIsHeldAgain()
    {
        using var harness = Blender(reposts: true);

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        Assert.True(harness.Down(Middle, 100, 100, 20));
        Assert.False(harness.Move(110, 100, 30), "Left + Middle has no command: nothing held, the move passes");
        Assert.True(harness.Up(Middle, 110, 100, 40));
        Assert.True(harness.Move(115, 100, 50), "Left again: Middle held, moves re-posted");
        Assert.True(harness.Up(Left, 115, 100, 60));
        Assert.True(harness.KeyUp(KeyCode.Space, 70));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Middle@100,100", "up Middle", "down Middle@110,100", "drag Middle@115,100 by 5,0", "up Middle"], harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void Reposting_DragsAreOfTheOutputButton()
    {
        using var harness = Blender(reposts: true);

        Assert.True(harness.KeyDown(KeyCode.D, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        Assert.True(harness.Move(104, 99, 20));
        Assert.True(harness.Up(Left, 104, 99, 30));
        Assert.True(harness.KeyUp(KeyCode.D, 40));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Alt+X2@100,100", "drag X2@104,99 by 4,-1", "up X2"], harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void ASpaceTapWithinTheTapTime_TypesASpace()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyUp(KeyCode.Space, 180));
        harness.WaitForLog(LogSources.Hold, "Tap sent");

        Assert.Equal(["press Space", "release Space"], harness.Simulator.All);
        Assert.True(harness.Log.Has(LogSources.Hold, "Hold"));
    }

    [Fact]
    public void ALongSpace_SendsNothing_SoBlenderNeverStartsPlayback()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyDown(KeyCode.Space, 100), "a repeat is swallowed too");
        Assert.True(harness.KeyUp(KeyCode.Space, 300));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Empty(harness.Simulator.All);
        Assert.Equal("held longer than the tap time (180 ms)", Property(harness.Log.Single(LogSources.Hold, "Tap not sent"), "reason"));
    }

    [Fact]
    public void ARollover_TypesSpaceThenTheKey_AndAKeyPressedBeforeTheWorkerCaughtUpWaitsItsTurn()
    {
        using var harness = Blender();
        // The worker stays inside the tap until released, as a busy worker would: "b" comes while the replays are queued.
        harness.Simulator.BlockOn = "press Space";

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyDown(KeyCode.A, 50), "typing: claimed for the rollover");
        EngineHarness.WaitFor(() => harness.Simulator.All.Contains("press Space"), "the worker inside the tap");
        Assert.True(harness.KeyUp(KeyCode.Space, 60));
        Assert.True(harness.KeyUp(KeyCode.A, 70));
        Assert.True(harness.KeyDown(KeyCode.B, 80), "b would overtake the queued space and a");
        Assert.True(harness.KeyUp(KeyCode.B, 90));
        harness.Simulator.Unblock();
        EngineHarness.WaitFor(() => harness.Host.PendingHoldReplays == 0, "the worker to catch up");

        Assert.Equal(["press Space", "release Space", "press A", "release A", "press B", "release B"], harness.Simulator.All);
        Assert.False(harness.KeyDown(KeyCode.C, 100), "caught up: keys pass through again");
        Assert.False(harness.KeyUp(KeyCode.C, 110));
        harness.WaitForLog(LogSources.Hold, "Key replayed in order");
        Assert.True(harness.Log.Has(LogSources.Hold, "Rollover"));
        AssertNoMismatch(harness);
    }

    [Fact]
    public void SpaceAndMiddle_WithMiddleAsTheStrokeButton_ZoomsAndNeverStartsAStroke()
    {
        var options = new EngineHostOptions(Middle, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1));
        using var harness = Blender(options: options);

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Middle, 100, 100, 10));
        for (var i = 1; i <= 20; i++)
        {
            harness.Move(100 + (i * 10), 100, 10 + (i * 10));
        }

        Assert.True(harness.Up(Middle, 300, 100, 230));
        Assert.True(harness.KeyUp(KeyCode.Space, 240));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Control+Middle@100,100", "up Middle"], harness.Simulator.All);
        Assert.Empty(harness.Trail.Calls);
        Assert.Empty(harness.Events);
        Assert.False(harness.Log.Has(LogSources.Capture, "Stroke began"));
    }

    [Fact]
    public void BlenderOnTheIgnoreList_PassesTheStrokeButton_AndStillHasItsHoldRemap()
    {
        using var harness = Blender(ignored: true);
        harness.Move(100, 100, 0);
        EngineHarness.WaitFor(() => harness.Host.IgnoredUnderPointer is not null, "the pointer over Blender, which is ignored");

        Assert.False(harness.Down(Right, 100, 100, 5), "the stroke button passes through over an ignored app");
        Assert.False(harness.Up(Right, 100, 100, 8));
        Assert.True(harness.KeyDown(KeyCode.Space, 10));
        Assert.True(harness.Down(Left, 100, 100, 20));
        Assert.True(harness.Up(Left, 100, 100, 30));
        Assert.True(harness.KeyUp(KeyCode.Space, 40));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["down Middle@100,100", "up Middle"], harness.Simulator.All);
    }

    [Fact]
    public void AStepsCommand_RunsOnTheExecutor_OnTheAppInFront()
    {
        using var harness = Blender();
        harness.Windows.Window = FakeWindowSystem.Identity("notepad.exe", handle: 0x10);

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyDown(KeyCode.Q, 20));
        Assert.True(harness.KeyUp(KeyCode.Q, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 60));
        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the Minimize step");
        harness.WaitForLog(LogSources.Execution, "Command fired");

        var call = Assert.Single(harness.WindowOperations.Calls);
        Assert.Equal(WindowOperation.Minimize, call.Operation);
        Assert.Same(BlenderHold.Window, call.Window);
        Assert.Equal("Space + Q", Property(harness.Log.Single(LogSources.Execution, "Hold command"), "trigger"));
        Assert.Empty(harness.Simulator.All);
    }

    [Fact]
    public void AKeyOutput_HoldsItsModifiersThroughThePress_AndMirrorsRepeats()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyDown(KeyCode.W, 10));
        Assert.True(harness.KeyDown(KeyCode.W, 40));
        Assert.True(harness.KeyUp(KeyCode.W, 50));
        Assert.True(harness.KeyDown(KeyCode.E, 60));
        Assert.True(harness.KeyUp(KeyCode.E, 70));
        Assert.True(harness.KeyUp(KeyCode.Space, 80));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["press G", "press G", "release G", "press LeftControl", "press S", "release S", "release LeftControl"], harness.Simulator.All);
    }

    [Fact]
    public void AWheelInput_TurnsItsOutputWithItsModifiers_OtherNotchesPass()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Wheel(WheelDirection.Up, 100, 100, 10));
        Assert.False(harness.Wheel(WheelDirection.Down, 100, 100, 20), "not an input: passes, but the tap is off");
        Assert.True(harness.KeyUp(KeyCode.Space, 30));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        Assert.Equal(["press LeftControl", "scroll Up x1@100,100", "release LeftControl"], harness.Simulator.All);
    }

    [Fact]
    public void StoppingWithAnOutputHeld_ReleasesIt()
    {
        var harness = Blender();
        try
        {
            Assert.True(harness.KeyDown(KeyCode.Space, 0));
            Assert.True(harness.Down(Left, 100, 100, 10));
            EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 1, "Middle down");

            harness.Host.Stop();

            Assert.Equal(["down Middle@100,100", "up Middle"], harness.Simulator.Mouse);
        }
        finally
        {
            harness.Dispose();
        }
    }

    [Fact]
    public void AnUnlock_ResetsTheHold_ReleasesItsOutput_AndTheNextSpaceStartsAfresh()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 1, "Middle down");
        harness.SystemEvents.Raise(SystemEventKind.SessionUnlocked);
        EngineHarness.WaitFor(() => harness.Simulator.Mouse.Count == 2, "the output released at the reset");
        harness.WaitForLog(LogSources.Capture, "Capture reset");

        // Forgotten, as every record is at a hook reset: what was physically held reaches the OS from here.
        Assert.False(harness.Up(Left, 100, 100, 20));
        Assert.False(harness.KeyUp(KeyCode.Space, 30));
        Assert.True(harness.KeyDown(KeyCode.Space, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 50));
        harness.WaitForLog(LogSources.Hold, "Tap sent");

        Assert.Equal(["down Middle@100,100", "up Middle", "press Space", "release Space"], harness.Simulator.All);
    }

    [Fact]
    public void KeysHeldLongWithoutRepeats_StayTheHolds_TheirReleasesAreSwallowed()
    {
        // macOS lets key repeat be off: Space and W then send nothing between press and release, far past the key record's
        // 2 s lost-release window, and their releases must still be the hold's (A19), not reach the OS as stray ups.
        using var harness = Blender();
        const long Late = 3 * KeySuppressionShadow.LostReleaseAfterMs;

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.KeyDown(KeyCode.W, 10));
        Assert.True(harness.Down(Left, 100, 100, 20));
        Assert.True(harness.Up(Left, 100, 100, Late / 2));
        Assert.True(harness.KeyUp(KeyCode.W, Late - 1000));
        Assert.True(harness.KeyUp(KeyCode.Space, Late));
        harness.WaitForLog(LogSources.Hold, "Tap not sent");

        // The hold ended with that release: a quick Space is a tap again.
        Assert.True(harness.KeyDown(KeyCode.Space, Late + 1000));
        Assert.True(harness.KeyUp(KeyCode.Space, Late + 1050));
        harness.WaitForLog(LogSources.Hold, "Tap sent");

        Assert.Equal(["press G", "down Middle@100,100", "up Middle", "release G", "press Space", "release Space"], harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void ANewForeground_ReachesTheHookAtOnce_WithoutWaitingForThePoll()
    {
        // The poll is an hour here: only the platform's notification can bring Blender's plan in time.
        var options = new EngineHostOptions(EngineHarness.StrokeButton, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1), FocusPollInterval: TimeSpan.FromHours(1));
        using var harness = new EngineHarness(options, mapping: BlenderHold.Document());
        harness.WaitForLog(LogSources.Hold, "Hold remaps watched");
        Assert.True(harness.Host.ForegroundHoldPlan.IsEmpty);
        Assert.False(harness.KeyDown(KeyCode.Space, 0), "nothing with hold remaps in front: Space types");
        Assert.False(harness.KeyUp(KeyCode.Space, 10));

        harness.Windows.ForegroundWindow = BlenderHold.Window;
        harness.SystemEvents.Raise(SystemEventKind.ForegroundChanged);
        EngineHarness.WaitFor(() => !harness.Host.ForegroundHoldPlan.IsEmpty, "Blender's hold remaps from the notification");

        Assert.True(harness.KeyDown(KeyCode.Space, 20));
        Assert.True(harness.KeyUp(KeyCode.Space, 30));
        harness.WaitForLog(LogSources.Hold, "Tap sent");
        // The watch publishes first and logs second, on its own thread.
        harness.WaitForLog(LogSources.Hold, "Foreground hold remaps");
        Assert.Equal("Blender", Property(harness.Log.From(LogSources.Hold).Last(e => e.Message == "Foreground hold remaps"), "app"));
    }

    private static object? Property(LogEvent e, string key) => e.Properties!.Single(p => p.Key == key).Value;

    [Fact]
    public void FocusMovingAwayMidHold_EndsAHoldWithNothingOwed_ItsReleaseStaysSwallowed_AndNoTap()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        SwitchTo(harness, Notepad);
        Assert.False(harness.Down(Left, 100, 100, 50), "the hold ended: Left reaches Notepad");
        Assert.False(harness.Up(Left, 100, 100, 60));
        Assert.True(harness.KeyUp(KeyCode.Space, 100), "its press was swallowed, so is its release");
        harness.WaitForLog(LogSources.Hold, "Hold ended: focus moved");

        // Back in Blender a quick Space is a tap: the only injection, so the release above sent nothing.
        SwitchTo(harness, BlenderHold.Window);
        Assert.True(harness.KeyDown(KeyCode.Space, 200));
        Assert.True(harness.KeyUp(KeyCode.Space, 250));
        harness.WaitForLog(LogSources.Hold, "Tap sent");

        Assert.Equal(["press Space", "release Space"], harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    [Fact]
    public void FocusMovingAwayWithAnInputHeld_KeepsFollowingIt_AndTheHoldEndsAfterTheLastRelease()
    {
        using var harness = Blender();

        Assert.True(harness.KeyDown(KeyCode.Space, 0));
        Assert.True(harness.Down(Left, 100, 100, 10));
        SwitchTo(harness, Notepad);
        Assert.True(harness.Down(Right, 110, 100, 20), "Left is still owed: the hold follows the set");
        Assert.True(harness.Up(Right, 120, 100, 30));
        Assert.True(harness.Up(Left, 130, 100, 40));
        Assert.True(harness.KeyUp(KeyCode.Space, 50));
        harness.WaitForLog(LogSources.Hold, "Hold ended: focus moved");

        Assert.Equal(
            [
                "down Middle@100,100",
                "up Middle", "down Control+Middle@110,100",
                "up Middle", "down Middle@120,100",
                "up Middle",
            ],
            harness.Simulator.All);
        AssertNoMismatch(harness);
    }

    /// <summary>Another app comes to the front, reported as the platform reports it; returns once the hook has its plan.</summary>
    private static void SwitchTo(EngineHarness harness, WindowIdentity window)
    {
        var expected = window == BlenderHold.Window;
        harness.Windows.ForegroundWindow = window;
        harness.SystemEvents.Raise(SystemEventKind.ForegroundChanged);
        EngineHarness.WaitFor(() => harness.Host.ForegroundHoldPlan.IsEmpty != expected, $"{window.ProcessName}'s hold remaps in front");
    }

    /// <summary>Blender in front and under the pointer, its plan published to the hook; <paramref name="reposts"/>: a simulator that asks for drags to be re-posted (as macOS's does).</summary>
    private static EngineHarness Blender(bool ignored = false, EngineHostOptions? options = null, bool reposts = false)
    {
        var harness = new EngineHarness(options, mapping: BlenderHold.Document(ignored), simulator: new FakeInputSimulator(reposts));
        harness.Windows.Window = BlenderHold.Window;
        harness.Windows.ForegroundWindow = BlenderHold.Window;
        EngineHarness.WaitFor(() => !harness.Host.ForegroundHoldPlan.IsEmpty, "Blender's hold remaps in front");
        return harness;
    }

    private static void AssertNoMismatch(EngineHarness harness)
    {
        Assert.False(harness.Log.Has(LogSources.Hold, "Hold decision mismatch"));
        Assert.False(harness.Log.Has(LogSources.Capture, "Suppression decision mismatch"));
    }
}
