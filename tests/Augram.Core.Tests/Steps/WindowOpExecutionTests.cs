using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.WindowOp;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class WindowOpExecutionTests
{
    private static readonly WindowOpStepType Type = WindowOpStepType.Instance;

    [Fact]
    public void NoTargetIsSkippedWithoutTouchingTheAdapter()
    {
        var windows = new FakeWindowOperations();

        var result = Type.Execute(new WindowOpStep(WindowOperation.Close), StepContexts.Create(target: null, windows: windows));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no window under the gesture start", result.Reason);
        Assert.Empty(windows.Calls);
    }

    [Fact]
    public void UnsupportedOperationIsSkippedWithThePlatformReason()
    {
        var windows = new FakeWindowOperations { Platform = HostPlatform.MacOS };
        windows.Unsupported.Add(WindowOperation.ToggleAlwaysOnTop);

        var result = Type.Execute(new WindowOpStep(WindowOperation.ToggleAlwaysOnTop), StepContexts.Create(StepContexts.Window(), windows));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("ToggleAlwaysOnTop is not supported on MacOS", result.Reason);
        Assert.Empty(windows.Calls);
    }

    [Fact]
    public void AdapterFailureIsFailedWithItsReason()
    {
        var windows = new FakeWindowOperations { Result = WindowOperationResult.Failed("SetWindowPos failed (5)") };

        var result = Type.Execute(new WindowOpStep(WindowOperation.Center), StepContexts.Create(StepContexts.Window(), windows));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("SetWindowPos failed (5)", result.Reason);
        Assert.Single(windows.Calls);
    }

    [Fact]
    public void AdapterFailureWithoutAReasonStillNamesTheOperation()
    {
        var windows = new FakeWindowOperations { Result = new WindowOperationResult(false) };

        var result = Type.Execute(new WindowOpStep(WindowOperation.Minimize), StepContexts.Create(StepContexts.Window(), windows));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("Minimize failed", result.Reason);
    }

    [Fact]
    public void SuccessIsDoneAndPerformGetsTheTarget()
    {
        var windows = new FakeWindowOperations();
        var target = StepContexts.Window("chrome.exe");

        var result = Type.Execute(new WindowOpStep(WindowOperation.SnapLeftHalf), StepContexts.Create(target, windows));

        Assert.True(result.Succeeded);
        Assert.Null(result.Reason);
        var call = Assert.Single(windows.Calls);
        Assert.Equal(WindowOperation.SnapLeftHalf, call.Operation);
        Assert.Same(target, call.Window);
        Assert.Null(call.Size);
    }

    [Fact]
    public void SetSizePassesTheSizeThrough()
    {
        var windows = new FakeWindowOperations();

        var result = Type.Execute(new WindowOpStep(WindowOperation.SetSize, new WindowSize(1280, 720)), StepContexts.Create(StepContexts.Window(), windows));

        Assert.True(result.Succeeded);
        var call = Assert.Single(windows.Calls);
        Assert.Equal(WindowOperation.SetSize, call.Operation);
        Assert.Equal(new WindowSize(1280, 720), call.Size);
    }

    [Fact]
    public void SetSizeWithoutASizeFailsBeforeTheAdapter()
    {
        var windows = new FakeWindowOperations();

        var result = Type.Execute(new WindowOpStep(WindowOperation.SetSize), StepContexts.Create(StepContexts.Window(), windows));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("Set size needs a width and a height", result.Reason);
        Assert.Empty(windows.Calls);
    }

    [Fact]
    public void EveryRunLogsOneDebugLineWithTheOutcome()
    {
        var log = new CountingEventLog();
        var windows = new FakeWindowOperations { Platform = HostPlatform.MacOS };
        windows.Unsupported.Add(WindowOperation.ToggleAlwaysOnTop);

        Type.Execute(new WindowOpStep(WindowOperation.ToggleAlwaysOnTop), StepContexts.Create(StepContexts.Window("finder"), windows, log: log));

        var entry = Assert.Single(log.Events);
        Assert.Equal(EventLevel.Debug, entry.Level);
        Assert.Equal("steps", entry.Source);
        Assert.Equal("Window operation", entry.Message);
        Assert.Contains(new LogProperty("operation", WindowOperation.ToggleAlwaysOnTop), entry.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Skipped), entry.Properties!);
        Assert.Contains(new LogProperty("reason", "ToggleAlwaysOnTop is not supported on MacOS"), entry.Properties!);
        Assert.Contains(new LogProperty("process", "finder"), entry.Properties!);
    }

    [Fact]
    public void NothingIsLoggedWhenDebugIsOff()
    {
        var log = new CountingEventLog(EventLevel.Info);

        Type.Execute(new WindowOpStep(WindowOperation.Close), StepContexts.Create(StepContexts.Window(), log: log));

        Assert.Equal(0, log.LogCalls);
    }
}
