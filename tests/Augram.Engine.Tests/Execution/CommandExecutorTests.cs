using System.Diagnostics;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Engine.Execution;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;
using Augram.Engine.Tests.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary>The executor on its own (no host): queue overflow, stop while a step blocks, enqueue after stop.</summary>
public sealed class CommandExecutorTests
{
    private static readonly GestureId Gesture = GestureId.New();

    [Fact]
    public void QueueFull_DropsTheOldest_AndWarnsOnce()
    {
        var log = new ListEventLog();
        using var executor = Executor(log, new DelayStep(10_000));

        executor.Enqueue(Request());
        EngineHarness.WaitFor(() => log.Has(LogSources.Execution, "Trigger resolved"), "the first request to start");
        for (var i = 0; i < CommandExecutor.QueueCapacity + 1; i++)
        {
            executor.Enqueue(Request());
        }

        var warnings = log.Events.Where(e => e.Message == "Execution queue full").ToList();
        var warning = Assert.Single(warnings);
        Assert.Equal(EventLevel.Warning, warning.Level);
        Assert.Equal(LogSources.Execution, warning.Source);
        Assert.Contains(warning.Properties!, p => p.Key == "dropped" && (string)p.Value! == "this gesture");
        executor.Stop();
        Assert.False(executor.IsRunning);
    }

    [Fact]
    public void Stop_WakesABlockingStep_DrainsWithoutRunning_AndJoinsTheThread()
    {
        var log = new ListEventLog();
        using var executor = Executor(log, new DelayStep(10_000));

        executor.Enqueue(Request());
        executor.Enqueue(Request());
        EngineHarness.WaitFor(() => log.Has(LogSources.Execution, "Trigger resolved"), "the first request to start");
        var started = Stopwatch.GetTimestamp();
        executor.Stop();

        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(4), "Stop must not wait out the delay");
        Assert.False(executor.IsRunning);
        Assert.True(log.Has(LogSources.Execution, "Command cancelled"));
        Assert.Single(log.Events, e => e.Message == "Trigger resolved");
        Assert.False(log.Has(LogSources.Execution, "Command fired"));
    }

    [Fact]
    public void Enqueue_AfterStop_IsDroppedQuietly()
    {
        var log = new ListEventLog();
        using var executor = Executor(log, new DelayStep(0));
        executor.Stop();

        executor.Enqueue(Request());

        Assert.True(log.Has(LogSources.Execution, "Execution request dropped: executor stopped"));
        Assert.False(log.Has(LogSources.Execution, "Trigger resolved"));
        executor.Stop();
    }

    private static CommandExecutor Executor(ListEventLog log, DelayStep step)
    {
        var mapping = Mappings.Global(Mappings.Command("Wait", Trigger.ForGesture(Gesture), step));
        var executor = new CommandExecutor(
            new EnginePorts { Input = new FakeInputSource(), Simulator = new FakeInputSimulator(), Log = log, Mapping = () => mapping },
            new EngineHostOptions(SettleDelayMs: 0));
        executor.Start();
        return executor;
    }

    private static ExecutionRequest Request() => new(PressedTrigger.Of(Trigger.ForGesture(Gesture)), new CapturePoint(1, 2, 0), null);
}
