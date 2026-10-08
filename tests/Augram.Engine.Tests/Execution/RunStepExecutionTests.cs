using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Run;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Augram.Engine.Tests.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>
/// The executor hands a Run step the launcher from <see cref="EnginePorts.ProcessLauncher"/> (a fake: nothing starts),
/// never activates the target for it, and without a launcher the step skips with the null object's reason. Waits for the
/// log line after the act (handoff §6: the executor acts first and logs second).
/// </summary>
public sealed class RunStepExecutionTests
{
    private static readonly GestureId Gesture = GestureId.New();

    [Fact]
    public void ARunStepStartsThroughThePortsLauncherWithoutActivating()
    {
        var log = new ListEventLog();
        var launcher = new FakeProcessLauncher();
        var windows = new FakeWindowSystem { Window = FakeWindowSystem.Identity("game.exe") };
        using var executor = Executor(log, new RunStep("taskkill.exe", "/f /im synthetic-emulator.exe", Elevated: true, Hidden: true), launcher, windows);

        executor.Enqueue(Request());

        EngineHarness.WaitFor(() => launcher.Launches.Count == 1, "the launch");
        EngineHarness.WaitFor(() => log.Has(LogSources.Execution, "Command fired"), "the command line");
        Assert.Equal(new ProcessLaunch("taskkill.exe", "/f /im synthetic-emulator.exe", string.Empty, Elevated: true, Hidden: true), launcher.Launches[0]);
        Assert.Empty(windows.Activations);
        var ran = log.Single(LogSources.Execution, "Step ran");
        Assert.Contains(ran.Properties!, p => p.Key == "outcome" && (StepOutcome)p.Value! == StepOutcome.Done);
    }

    [Fact]
    public void WithoutALauncherTheStepSkipsAndTheCommandStillFires()
    {
        var log = new ListEventLog();
        using var executor = Executor(log, new RunStep("explorer"), launcher: null, new FakeWindowSystem());

        executor.Enqueue(Request());

        EngineHarness.WaitFor(() => log.Has(LogSources.Execution, "Command fired"), "the command line");
        var ran = log.Single(LogSources.Execution, "Step ran");
        Assert.Contains(ran.Properties!, p => p.Key == "outcome" && (StepOutcome)p.Value! == StepOutcome.Skipped);
        Assert.Contains(ran.Properties!, p => p.Key == "reason" && (string?)p.Value == NullProcessLauncher.Reason);
    }

    private static CommandExecutor Executor(ListEventLog log, RunStep step, FakeProcessLauncher? launcher, FakeWindowSystem windows)
    {
        var mapping = Mappings.Global(Mappings.Command("Run", Trigger.ForGesture(Gesture), step));
        var ports = new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator(), Log = log, Windows = windows, Mapping = () => mapping };
        var executor = new CommandExecutor(launcher is null ? ports : ports with { ProcessLauncher = launcher }, new EngineHostOptions(SettleDelayMs: 0));
        executor.Start();
        return executor;
    }

    private static ExecutionRequest Request() => new(Trigger.ForGesture(Gesture), new CapturePoint(1, 2, 0), null);
}
