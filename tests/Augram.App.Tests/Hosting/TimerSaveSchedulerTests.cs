using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class TimerSaveSchedulerTests
{
    [Fact]
    public void RunsOnceAfterTheDelay_ThroughTheMarshal()
    {
        var marshalled = 0;
        var scheduler = new TimerSaveScheduler(TimeSpan.FromMilliseconds(20), action =>
        {
            marshalled++;
            action();
        });
        using var ran = new ManualResetEventSlim();
        var runs = 0;

        scheduler.Schedule(() =>
        {
            runs++;
            ran.Set();
        });

        Assert.True(ran.Wait(TimeSpan.FromSeconds(30)));
        Thread.Sleep(60);
        Assert.Equal(1, runs);
        Assert.Equal(1, marshalled);
    }

    [Fact]
    public void DisposingBeforeTheDelayCancels()
    {
        var scheduler = new TimerSaveScheduler(TimeSpan.FromMilliseconds(30));
        var runs = 0;

        scheduler.Schedule(() => runs++).Dispose();
        Thread.Sleep(120);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void DisposingAfterTheTimerFiredButBeforeTheMarshalRanSkipsTheAction()
    {
        Action? deferred = null;
        using var captured = new ManualResetEventSlim();
        var scheduler = new TimerSaveScheduler(TimeSpan.Zero, action =>
        {
            deferred = action;
            captured.Set();
        });
        var runs = 0;

        var pending = scheduler.Schedule(() => runs++);
        Assert.True(captured.Wait(TimeSpan.FromSeconds(30)));
        pending.Dispose();
        deferred!();

        Assert.Equal(0, runs);
    }
}
