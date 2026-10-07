using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Augram.Engine.Tests.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// Recognised gesture or wheel tick → intercept → executor → window under the start → resolution →
/// activation → steps, end to end through the harness. The executor acts first and logs second, so
/// every test waits for the fake's recorded call or the log line before asserting.
/// </summary>
public sealed class CommandExecutionTests
{
    private static readonly Gesture Right = EngineHarness.LineGesture("right", 200, 0);
    private static readonly Gesture Down = EngineHarness.LineGesture("down", 0, 200);

    [Fact]
    public void GlobalCommand_RunsOnTheWindowUnderTheStart_AndCompletesTheEntry()
    {
        var mapping = Mappings.Global(Mappings.Command("Minimize", Trigger.ForGesture(Right.Id), new WindowOpStep(WindowOperation.Minimize)));
        using var harness = new EngineHarness(gestures: [Right], mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("notepad.exe", handle: 0x10, root: 0x20);
        harness.Windows.ActivationResult = new ActivationResult(true, "attach-thread-input", 7);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the window operation");
        var call = harness.WindowOperations.Calls[0];
        Assert.Equal(WindowOperation.Minimize, call.Operation);
        Assert.Equal(0x20, call.Window.RootHandle);
        harness.WaitForLog(LogSources.Execution, "Command fired");

        Assert.True(harness.Host.HasExecutor);
        Assert.Equal((100, 100), Assert.Single(harness.Windows.Lookups));
        Assert.Empty(harness.Windows.Activations); // a window operation acts on the handle; no activation, no Alt tap
        var entry = Assert.Single(harness.RecognitionLog.Snapshot());
        Assert.Equal("Global", entry.MatchedGroup);
        Assert.Equal("Minimize", entry.FiredCommand);
        Assert.Null(entry.NothingFiredReason);
        Assert.Equal(Right.Id, entry.MatchedGesture);
        Assert.Null(harness.Health.Current().LastActivationOutcome);
        var fired = harness.Log.Single(LogSources.Execution, "Command fired");
        Assert.Contains(fired.Properties!, p => p.Key == "stepsRun" && (int)p.Value! == 1);
        Assert.Contains(fired.Properties!, p => p.Key == "process" && (string)p.Value! == "notepad.exe");
        Assert.Contains(fired.Properties!, p => p.Key == "trigger" && (string)p.Value! == "gesture 'right'");
        Assert.False(harness.Log.Has(LogSources.Execution, "Window activated"));
    }

    [Fact]
    public void AppGroupOverrideToNothing_PerformsNothing_AndSaysSo()
    {
        var mapping = Mappings.Document(
            [Mappings.Command("Close", Trigger.ForGesture(Right.Id), new WindowOpStep(WindowOperation.Close))],
            Mappings.Group("Steam", "game.exe", commands: Mappings.Command("Nothing", Trigger.ForGesture(Right.Id))));
        using var harness = new EngineHarness(gestures: [Right], mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity("game.exe");

        harness.Stroke(200, 0);

        harness.WaitForRecognitionLog(1);
        harness.WaitForLog(LogSources.Execution, "Trigger resolved");
        var entry = Assert.Single(harness.RecognitionLog.Snapshot());
        Assert.Equal("Steam", entry.MatchedGroup);
        Assert.Null(entry.FiredCommand);
        Assert.Contains("override to nothing", entry.NothingFiredReason);
        Assert.Empty(harness.WindowOperations.Calls);
        Assert.Empty(harness.Windows.Activations);
        Assert.False(harness.Log.Has(LogSources.Execution, "Command fired"));
    }

    [Fact]
    public void Intercepted_ExecutesNothing_AndTheEntrySaysConsumed()
    {
        var mapping = Mappings.Global(Mappings.Command("Minimize", Trigger.ForGesture(Right.Id), new WindowOpStep(WindowOperation.Minimize)));
        var seen = new List<EngineEvent>();
        using var harness = new EngineHarness(gestures: [Right], mapping: mapping, intercept: e =>
        {
            seen.Add(e);
            return true;
        });
        harness.Windows.Window = FakeWindowSystem.Identity();

        harness.Stroke(200, 0);

        harness.WaitForLog(LogSources.Capture, "Stroke consumed");
        harness.WaitForEvents(1);
        Assert.Equal(EngineWorker.ConsumedReason, Assert.Single(harness.RecognitionLog.Snapshot()).NothingFiredReason);
        Assert.IsType<EngineEvent.GestureRecognized>(Assert.Single(seen));
        Assert.Empty(harness.Windows.Lookups);
        Assert.Empty(harness.WindowOperations.Calls);
        Assert.False(harness.Log.Has(LogSources.Execution, "Trigger resolved"));
    }

    [Fact]
    public void NoMappingPort_HasNoExecutor_AndTheEntrySaysSo()
    {
        using var harness = new EngineHarness(gestures: [Right]);
        harness.Windows.Window = FakeWindowSystem.Identity();

        harness.Stroke(200, 0);

        harness.WaitForRecognitionLog(1);
        Assert.False(harness.Host.HasExecutor);
        Assert.False(harness.Host.ExecutorRunning);
        Assert.Equal(EngineWorker.NoMappingReason, Assert.Single(harness.RecognitionLog.Snapshot()).NothingFiredReason);
        Assert.Empty(harness.Windows.Lookups);
    }

    [Fact]
    public void FailedStep_StopsTheCommand_BeforeTheNextStep()
    {
        var first = new FakeStepType(StepCategory.System) { Result = StepResult.Failed("no can do") };
        var second = new FakeStepType(StepCategory.System);
        var mapping = Mappings.Global(Mappings.Command("Two steps", Trigger.ForGesture(Right.Id), first.Step, second.Step));
        using var harness = new EngineHarness(gestures: [Right], mapping: mapping);

        harness.Stroke(200, 0);

        harness.WaitForLog(LogSources.Execution, "Command stopped");
        Assert.Single(first.Runs);
        Assert.Empty(second.Runs);
        Assert.False(harness.Log.Has(LogSources.Execution, "Command fired"));
        var stopped = harness.Log.Single(LogSources.Execution, "Command stopped");
        Assert.Contains(stopped.Properties!, p => p.Key == "reason" && (string)p.Value! == "no can do");
        Assert.Contains(stopped.Properties!, p => p.Key == "step" && (int)p.Value! == 0);
    }

    [Fact]
    public void OnTheOtherPlatform_StepsRunConverted_AndOneWithNoGuessIsSkippedWithItsReason()
    {
        var noGuess = new FakeStepType(StepCategory.System) { Converts = _ => StepConversion.None("Win+D needs a macOS version") };
        var replacement = new FakeStepType(StepCategory.System);
        var converted = new FakeStepType(StepCategory.System) { Converts = _ => StepConversion.To(replacement.Step) };
        var mapping = Mappings.Global(Mappings.Command("Authored on Windows", Trigger.ForGesture(Right.Id), noGuess.Step, converted.Step));
        using var harness = new EngineHarness(gestures: [Right], mapping: mapping);
        harness.WindowOperations.Platform = HostPlatform.MacOS;

        harness.Stroke(200, 0);
        harness.WaitForLog(LogSources.Execution, "Command fired");

        Assert.Empty(noGuess.Runs);
        Assert.Empty(converted.Runs);
        Assert.Single(replacement.Runs);
        var skipped = harness.Log.Single(LogSources.Execution, "Step skipped");
        Assert.Contains(skipped.Properties!, p => p.Key == "reason" && (string)p.Value! == "Win+D needs a macOS version");
        var fired = harness.Log.Single(LogSources.Execution, "Command fired");
        Assert.Contains(fired.Properties!, p => p.Key == "stepsRun" && (int)p.Value! == 1);
        Assert.Contains(fired.Properties!, p => p.Key == "stepsSkipped" && (int)p.Value! == 1);
    }

    [Fact]
    public void OnThePlatformWithItsOwnSteps_ThoseRunInsteadOfTheConvertedOriginal()
    {
        var original = new FakeStepType(StepCategory.System);
        var own = new FakeStepType(StepCategory.System);
        var command = Mappings.Command("Delete word", Trigger.ForGesture(Right.Id), original.Step)
            .WithStepsFor(HostPlatform.MacOS, [new CommandStep(own.Step, HostPlatform.MacOS)], DateTimeOffset.UnixEpoch);
        using var harness = new EngineHarness(gestures: [Right], mapping: Mappings.Global(command));
        harness.WindowOperations.Platform = HostPlatform.MacOS;

        harness.Stroke(200, 0);
        harness.WaitForLog(LogSources.Execution, "Command fired");

        Assert.Empty(original.Runs);
        Assert.Single(own.Runs);
    }

    [Fact]
    public void InactiveSteps_AreSkipped_AndACommandWithNoneLogsIt()
    {
        var inactive = new FakeStepType(StepCategory.System);
        var active = new FakeStepType(StepCategory.System);
        var mixed = Mappings.Command("Mixed", Trigger.ForGesture(Right.Id), inactive.Step, active.Step);
        mixed = mixed with { Steps = [mixed.Steps[0] with { IsActive = false }, mixed.Steps[1]] };
        var none = Mappings.Command("None", Trigger.ForGesture(Down.Id), inactive.Step);
        none = none with { Steps = [none.Steps[0] with { IsActive = false }] };
        using var harness = new EngineHarness(gestures: [Right, Down], mapping: Mappings.Global(mixed, none));

        harness.Stroke(200, 0);
        harness.WaitForLog(LogSources.Execution, "Command fired");
        harness.Stroke(0, 200, startMs: 1000);
        harness.WaitForLog(LogSources.Execution, "Command has no active steps");

        Assert.Empty(inactive.Runs);
        Assert.Single(active.Runs);
        Assert.Equal(2, harness.RecognitionLog.Count);
        Assert.Equal("None", harness.RecognitionLog.Snapshot()[1].FiredCommand);
    }

    [Fact]
    public void WheelTrigger_BoundToAMediaKey_PlaysItThroughTheSimulator()
    {
        var mapping = Mappings.Global(Mappings.Command("Louder", Trigger.ForWheel(WheelDirection.Up), new MediaKeyStep(MediaKeyKind.VolumeUp)));
        using var harness = new EngineHarness(mapping: mapping);

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        harness.WaitForState(CaptureState.Held);
        Assert.True(harness.Wheel(WheelDirection.Up, 10, 10, 20));
        harness.WaitForState(CaptureState.WheelFiring);
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 60));

        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 2, "the media key press and release");
        Assert.Equal(["press VolumeUp", "release VolumeUp"], harness.Simulator.Keys);
        harness.WaitForLog(LogSources.Execution, "Command fired");
        harness.WaitForState(CaptureState.Idle);
        Assert.Equal((10, 10), Assert.Single(harness.Windows.Lookups));
        Assert.Empty(harness.RecognitionLog.Snapshot());
    }

    [Fact]
    public void WheelTrigger_BoundToARightHandHotkey_SendsTheRightHandSetToTheSimulator()
    {
        var mapping = Mappings.Global(Mappings.Command("Ctrl+RAlt+F9", Trigger.ForWheel(WheelDirection.Up), new HotkeyStep(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt)));
        using var harness = new EngineHarness(mapping: mapping);

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        harness.WaitForState(CaptureState.Held);
        Assert.True(harness.Wheel(WheelDirection.Up, 10, 10, 20));
        harness.WaitForState(CaptureState.WheelFiring);
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 60));

        EngineHarness.WaitFor(() => harness.Simulator.Keys.Count == 1, "the hotkey");
        Assert.Equal(["hotkey Control, Alt+F9 right Alt"], harness.Simulator.Keys);
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    [Fact]
    public void ThrowingStep_IsLoggedAsAnError_AndTheNextRequestStillRuns()
    {
        var throwing = new FakeStepType(StepCategory.System) { Throws = new InvalidOperationException("boom") };
        var mapping = Mappings.Global(
            Mappings.Command("Throws", Trigger.ForGesture(Right.Id), throwing.Step),
            Mappings.Command("Minimize", Trigger.ForGesture(Down.Id), new WindowOpStep(WindowOperation.Minimize)));
        using var harness = new EngineHarness(gestures: [Right, Down], mapping: mapping);
        harness.Windows.Window = FakeWindowSystem.Identity();

        harness.Stroke(200, 0);
        harness.WaitForLog(LogSources.Execution, "Command execution failed");
        harness.Stroke(0, 200, startMs: 1000);

        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the second command's window operation");
        harness.WaitForLog(LogSources.Execution, "Command fired");
        Assert.True(harness.Host.ExecutorRunning);
        Assert.Single(throwing.Runs);
        var error = harness.Log.Single(LogSources.Execution, "Command execution failed");
        Assert.IsType<InvalidOperationException>(error.Exception);
    }
}
