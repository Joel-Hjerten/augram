using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class OneShotTimerTests
{
    [Fact]
    public void RunsOnceAfterTheDelay_ThroughTheMarshal()
    {
        var marshalled = 0;
        var timer = new OneShotTimer(action =>
        {
            marshalled++;
            action();
        });
        using var ran = new ManualResetEventSlim();
        var runs = 0;

        timer.Schedule(TimeSpan.FromMilliseconds(20), () =>
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
        var timer = new OneShotTimer();
        var runs = 0;

        timer.Schedule(TimeSpan.FromMilliseconds(30), () => runs++).Dispose();
        Thread.Sleep(120);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void DisposingAfterTheTimerFiredButBeforeTheMarshalRanSkipsTheAction()
    {
        Action? deferred = null;
        using var captured = new ManualResetEventSlim();
        var timer = new OneShotTimer(action =>
        {
            deferred = action;
            captured.Set();
        });
        var runs = 0;

        var pending = timer.Schedule(TimeSpan.Zero, () => runs++);
        Assert.True(captured.Wait(TimeSpan.FromSeconds(30)));
        pending.Dispose();
        deferred!();

        Assert.Equal(0, runs);
    }

    [Fact]
    public void RestartingRunsOnlyTheLastRequest()
    {
        var timer = new OneShotTimer();
        using var ran = new ManualResetEventSlim();
        var first = 0;
        var second = 0;

        timer.Schedule(TimeSpan.FromMilliseconds(30), () => first++).Dispose();
        timer.Schedule(TimeSpan.FromMilliseconds(30), () =>
        {
            second++;
            ran.Set();
        });

        Assert.True(ran.Wait(TimeSpan.FromSeconds(30)));
        Thread.Sleep(60);
        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }
}
