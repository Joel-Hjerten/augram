using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.WindowOp;
using Augram.Engine.Tests.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// A8 and lazy activation: the target is activated right before the first Keyboard, Mouse or Text step, or the
/// first Window step that keeps the window on screen (never for a command of other System steps only, nor
/// a minimize or close), and the settle delay is waited once before the first injected step when the
/// activation moved focus, never before a window operation. Measured as the gap
/// between the fake window system's activation timestamp and the fake step's run timestamp, with a
/// generous tolerance for the CI runner.
/// </summary>
public sealed class SettleDelayTests
{
    private static readonly Gesture Right = EngineHarness.LineGesture("right", 200, 0);
    private static readonly ActivationResult Moved = new(true, "set-foreground", 2);

    [Fact]
    public void KeyboardStepFirst_AfterFocusMoved_WaitsTheSettleDelay()
    {
        var keyboard = new FakeStepType(StepCategory.Keyboard);
        using var harness = Harness([keyboard], settleDelayMs: 50, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => keyboard.Runs.Count == 1, "the keyboard step");
        var waited = GapMs(Assert.Single(harness.Windows.Activations).Timestamp, keyboard.Runs[0]);
        Assert.True(waited >= 40, $"waited {waited:F1} ms, expected at least 50");
        harness.WaitForLog(LogSources.Execution, "Command fired");
        Assert.True(harness.Log.Has(LogSources.Execution, "Window activated"));
        Assert.Equal("set-foreground (2 ms)", harness.Health.Current().LastActivationOutcome);
    }

    [Fact]
    public void SystemStepsOnly_NeverActivateOrWait()
    {
        var system = new FakeStepType(StepCategory.System);
        using var harness = Harness([system], settleDelayMs: 1500, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => system.Runs.Count == 1, "the system step");
        harness.WaitForLog(LogSources.Execution, "Command fired");
        Assert.Empty(harness.Windows.Activations);
    }

    [Fact]
    public void KeyboardStepFirst_WhenFocusDidNotMove_DoesNotWait()
    {
        var keyboard = new FakeStepType(StepCategory.Keyboard);
        using var harness = Harness([keyboard], settleDelayMs: 1500, activation: ActivationResult.NotNeeded);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => keyboard.Runs.Count == 1, "the keyboard step");
        var waited = GapMs(Assert.Single(harness.Windows.Activations).Timestamp, keyboard.Runs[0]);
        Assert.True(waited < 1000, $"waited {waited:F1} ms, expected no settle delay");
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    [Fact]
    public void MouseStepFirst_AfterFocusMoved_WaitsTheSettleDelay()
    {
        // A Scroll's held keys go to the foreground window and its wheel must reach the same one.
        var mouse = new FakeStepType(StepCategory.Mouse);
        using var harness = Harness([mouse], settleDelayMs: 50, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => mouse.Runs.Count == 1, "the mouse step");
        var waited = GapMs(Assert.Single(harness.Windows.Activations).Timestamp, mouse.Runs[0]);
        Assert.True(waited >= 40, $"waited {waited:F1} ms, expected at least 50");
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    [Fact]
    public void ActivationAndTheDelayHappenOnce_BeforeTheFirstKeyboardStepOnly()
    {
        var system = new FakeStepType(StepCategory.System);
        var first = new FakeStepType(StepCategory.Keyboard);
        var second = new FakeStepType(StepCategory.Text);
        using var harness = Harness([system, first, second], settleDelayMs: 300, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => second.Runs.Count == 1, "the last step");
        var activated = Assert.Single(harness.Windows.Activations).Timestamp;
        Assert.True(system.Runs[0] <= activated, "the system step runs before the activation, without waiting for it");
        Assert.True(GapMs(activated, first.Runs[0]) >= 250, "the first keyboard step waits");
        Assert.True(GapMs(first.Runs[0], second.Runs[0]) < 250, "the second keyboard-class step does not wait again");
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    [Fact]
    public void MaximizeFirst_ActivatesTheTarget_WithoutWaiting()
    {
        // SP.net activates before every action; a window maximized from behind the focused one stays behind it otherwise (Joel, 2026-10-13).
        using var harness = Harness([new WindowOpStep(WindowOperation.MaximizeOrRestore)], settleDelayMs: 1500, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the window operation");
        var activated = Assert.Single(harness.Windows.Activations).Timestamp;
        Assert.True(GapMs(activated, harness.WindowOperations.Calls[0].Timestamp) < 1000, "no settle delay before a window operation");
        harness.WaitForLog(LogSources.Execution, "Command fired");
        Assert.True(harness.Log.Has(LogSources.Execution, "Window activated"));
    }

    [Fact]
    public void MinimizeFirst_NeverActivates()
    {
        using var harness = Harness([new WindowOpStep(WindowOperation.Minimize)], settleDelayMs: 1500, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => harness.WindowOperations.Calls.Count == 1, "the window operation");
        harness.WaitForLog(LogSources.Execution, "Command fired");
        Assert.Empty(harness.Windows.Activations);
    }

    [Fact]
    public void KeyboardStepAfterMaximize_WaitsTheSettleDelayOnce()
    {
        var keyboard = new FakeStepType(StepCategory.Keyboard);
        using var harness = Harness([new WindowOpStep(WindowOperation.MaximizeOrRestore), keyboard.Step], settleDelayMs: 300, activation: Moved);

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => keyboard.Runs.Count == 1, "the keyboard step");
        var activated = Assert.Single(harness.Windows.Activations).Timestamp;
        Assert.True(GapMs(activated, harness.WindowOperations.Calls[0].Timestamp) < 250, "the window operation does not wait");
        Assert.True(GapMs(activated, keyboard.Runs[0]) >= 250, "the first keyboard step waits");
        harness.WaitForLog(LogSources.Execution, "Command fired");
    }

    private static double GapMs(long from, long to) => Stopwatch.GetElapsedTime(from, to).TotalMilliseconds;

    private static EngineHarness Harness(FakeStepType[] steps, int settleDelayMs, ActivationResult activation) =>
        Harness(steps.Select(step => (IStep)step.Step).ToArray(), settleDelayMs, activation);

    private static EngineHarness Harness(IStep[] steps, int settleDelayMs, ActivationResult activation)
    {
        var command = Mappings.Command("Keys", Trigger.ForGesture(Right.Id), steps);
        var harness = new EngineHarness(gestures: [Right], mapping: Mappings.Global(command), settleDelayMs: settleDelayMs);
        harness.Windows.Window = FakeWindowSystem.Identity();
        harness.Windows.ActivationResult = activation;
        return harness;
    }
}
