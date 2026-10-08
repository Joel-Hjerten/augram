using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Run;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>Every branch of the Run step's executor, over <see cref="FakeProcessLauncher"/>: nothing is ever started.</summary>
public sealed class RunExecutionTests
{
    private static readonly RunStepType Type = RunStepType.Instance;

    [Fact]
    public void NoProgramIsSkippedWithoutTouchingTheLauncher()
    {
        var launcher = new FakeProcessLauncher();

        var result = Type.Execute(new RunStep("  ", "/x"), Context(launcher));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no program set", result.Reason);
        Assert.Empty(launcher.Launches);
    }

    [Fact]
    public void StartedIsDoneAndTheLauncherGetsTheTrimmedRequest()
    {
        var launcher = new FakeProcessLauncher();

        var result = Type.Execute(new RunStep(" taskkill.exe ", "/f /im yuzu.exe ", Elevated: true, Hidden: true), Context(launcher));

        Assert.True(result.Succeeded);
        Assert.Null(result.Reason);
        Assert.Equal(new ProcessLaunch("taskkill.exe", "/f /im yuzu.exe", string.Empty, Elevated: true, Hidden: true), Assert.Single(launcher.Launches));
    }

    [Fact]
    public void ADeclinedPromptIsSkippedAsCancelledSoTheCommandGoesOn()
    {
        var launcher = new FakeProcessLauncher { Result = ProcessLaunchResult.Cancelled("the administrator prompt was declined") };

        var result = Type.Execute(new RunStep("taskkill.exe", Elevated: true), Context(launcher));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled: the administrator prompt was declined", result.Reason);
    }

    [Fact]
    public void NotSupportedIsSkippedWithTheLaunchersReason()
    {
        var launcher = new FakeProcessLauncher { Result = ProcessLaunchResult.NotSupported("running as administrator is not supported on macOS") };

        var result = Type.Execute(new RunStep("/usr/bin/true", Elevated: true), Context(launcher));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("running as administrator is not supported on macOS", result.Reason);
    }

    [Fact]
    public void FailedStopsTheCommandWithTheLaunchersReason()
    {
        var launcher = new FakeProcessLauncher { Result = ProcessLaunchResult.Failed("explorer2 was not found (2)") };

        var result = Type.Execute(new RunStep("explorer2"), Context(launcher));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("explorer2 was not found (2)", result.Reason);
    }

    [Fact]
    public void AnswersWithoutAReasonStillSaySomething()
    {
        var launcher = new FakeProcessLauncher { Result = new ProcessLaunchResult(ProcessLaunchOutcome.Failed) };
        Assert.Equal("Run explorer2 could not be started", Type.Execute(new RunStep("explorer2", "secret"), Context(launcher)).Reason);

        launcher.Result = new ProcessLaunchResult(ProcessLaunchOutcome.Cancelled);
        Assert.Equal("cancelled", Type.Execute(new RunStep("explorer2"), Context(launcher)).Reason);

        launcher.Result = new ProcessLaunchResult(ProcessLaunchOutcome.NotSupported);
        Assert.Equal("Open ms-settings:display is not supported here", Type.Execute(new RunStep("ms-settings:display"), Context(launcher)).Reason);
    }

    [Fact]
    public void TheNullLauncherDeclinesAndTheStepSkips()
    {
        var result = Type.Execute(new RunStep("explorer"), StepContexts.Create());

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal(NullProcessLauncher.Reason, result.Reason);
    }

    [Fact]
    public void NothingStartsOnceTheExecutorIsStopping()
    {
        var launcher = new FakeProcessLauncher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = Type.Execute(new RunStep("explorer"), StepContexts.Create(cancellation: cancellation.Token) with { Processes = launcher });

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled", result.Reason);
        Assert.Empty(launcher.Launches);
    }

    [Fact]
    public void EveryRunLogsOneDebugLineWithTheFileButNeverTheArguments()
    {
        var log = new CountingEventLog();
        var launcher = new FakeProcessLauncher { Result = new ProcessLaunchResult(ProcessLaunchOutcome.Started, "still starting after 2 s") };

        Type.Execute(new RunStep("tool.exe", "--password hunter2", Elevated: true), StepContexts.Create(log: log) with { Processes = launcher });

        var entry = Assert.Single(log.Events);
        Assert.Equal(EventLevel.Debug, entry.Level);
        Assert.Equal("steps", entry.Source);
        Assert.Equal("Run", entry.Message);
        Assert.Contains(new LogProperty("file", "tool.exe"), entry.Properties!);
        Assert.Contains(new LogProperty("elevated", true), entry.Properties!);
        Assert.Contains(new LogProperty("hidden", false), entry.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), entry.Properties!);
        Assert.Contains(new LogProperty("reason", "still starting after 2 s"), entry.Properties!);
        Assert.DoesNotContain(entry.Properties!, property => property.Value is string text && text.Contains("hunter2", StringComparison.Ordinal));
    }

    [Fact]
    public void NothingIsLoggedWhenDebugIsOff()
    {
        var log = new CountingEventLog(EventLevel.Info);

        Type.Execute(new RunStep("explorer"), StepContexts.Create(log: log) with { Processes = new FakeProcessLauncher() });

        Assert.Equal(0, log.LogCalls);
    }

    private static StepExecutionContext Context(FakeProcessLauncher launcher) => StepContexts.Create() with { Processes = launcher };
}
