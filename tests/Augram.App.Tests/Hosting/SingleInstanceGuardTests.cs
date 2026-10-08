using Augram.App.Hosting;
using Xunit;
using static Augram.App.Tests.Support.InstanceTesting;

namespace Augram.App.Tests.Hosting;

/// <summary>
/// The running side of one Augram at a time: the mutex, the listener and the pipe protocol, both new and as Augram 0.1
/// spoke it. Real mutex and pipe objects under a unique name per test, never the app's own.
/// </summary>
public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void SecondAcquireFailsAndAnOldStyleSignalReachesTheFirst()
    {
        var name = UniqueName();
        using var shown = new ManualResetEventSlim();
        using var first = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(first);
        first.ShowRequested += (_, _) => shown.Set();

        using var second = SingleInstanceGuard.TryAcquire(name, Dev);
        Assert.Null(second);

        // Augram 0.1's second launch: one byte, no answer.
        Assert.True(SingleInstanceGuard.SignalExisting(name, TimeSpan.FromSeconds(5)));
        Assert.True(shown.Wait(Wait), "the first instance was not asked to show its window");
    }

    [Fact]
    public void AnAugram01SecondLaunchStillShowsTheNewListener()
    {
        var name = UniqueName();
        using var shown = new ManualResetEventSlim();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        InstanceRequestEventArgs? asked = null;
        running.ShowRequested += (_, e) =>
        {
            asked = e;
            shown.Set();
        };

        SignalLikeAugram01(name);

        Assert.True(shown.Wait(Wait), "a 0.1 second launch did not show the running Augram");
        Assert.Null(asked?.From);
    }

    [Fact]
    public void NameIsFreeAgainAfterDispose()
    {
        var name = UniqueName();
        var first = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(first);
        first.Dispose();

        using var again = SingleInstanceGuard.TryAcquire(name, Dev);

        Assert.NotNull(again);
    }

    [Fact]
    public async Task NameIsFreedByADisposeOnAnotherThread()
    {
        // A mutex belongs to the thread that took it; the guard's own thread holds it, so any thread may let it go.
        var name = UniqueName();
        var first = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(first);

        await Task.Run(first.Dispose);
        using var again = SingleInstanceGuard.TryAcquire(name, Dev);

        Assert.NotNull(again);
    }

    [Fact]
    public void SignalReportsFalseWhenNobodyListens()
    {
        var name = UniqueName();

        Assert.False(SingleInstanceGuard.SignalExisting(name, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(PeerAnswerKind.NoAnswer, SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Hello, Dev), TimeSpan.FromMilliseconds(200)).Kind);
    }

    [Fact]
    public void HelloIsAnsweredWithTheRunningIdentity_AndShowsNothing()
    {
        var name = UniqueName();
        var odd = new InstanceIdentity(new AppInfo("0.3.0-beta", null, AppChannel.Release), ExecutableIn("Program Files \"quoted\" Åäö\nline"));
        using var running = SingleInstanceGuard.TryAcquire(name, odd);
        Assert.NotNull(running);
        using var introduced = new ManualResetEventSlim();
        InstanceRequestEventArgs? asked = null;
        var shows = 0;
        running.Introduced += (_, e) =>
        {
            asked = e;
            introduced.Set();
        };
        running.ShowRequested += (_, _) => Interlocked.Increment(ref shows);

        var answer = SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Hello, Dev), TimeSpan.FromSeconds(5));

        Assert.Equal(PeerAnswerKind.Answered, answer.Kind);
        Assert.Equal(odd, answer.Running);
        Assert.True(introduced.Wait(Wait));
        Assert.Equal(Dev, asked?.From);
        Assert.Equal(0, Volatile.Read(ref shows));
    }

    [Fact]
    public void ShowAndQuitCarryTheAskerAndItsReason()
    {
        var name = UniqueName();
        using var running = SingleInstanceGuard.TryAcquire(name, Installed);
        Assert.NotNull(running);
        using var shown = new ManualResetEventSlim();
        using var quit = new ManualResetEventSlim();
        InstanceRequestEventArgs? show = null;
        InstanceRequestEventArgs? stop = null;
        running.ShowRequested += (_, e) =>
        {
            show = e;
            shown.Set();
        };
        running.QuitRequested += (_, e) =>
        {
            stop = e;
            quit.Set();
        };

        var showAnswer = SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Show, Dev, "same install"), TimeSpan.FromSeconds(5));
        Assert.True(shown.Wait(Wait));
        var quitAnswer = SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Quit, OtherDev), TimeSpan.FromSeconds(5));
        Assert.True(quit.Wait(Wait));

        Assert.Equal(Installed, showAnswer.Running);
        Assert.Equal(Installed, quitAnswer.Running);
        Assert.Equal(Dev, show?.From);
        Assert.Equal("same install", show?.Reason);
        Assert.Equal(OtherDev, stop?.From);
    }

    [Fact]
    public async Task AnAugram01ListenerIsAskedTheOldWay_AndShowsItself()
    {
        var name = UniqueName();
        using var cancel = new CancellationTokenSource(Wait);
        var connected = ListenLikeAugram01(name, cancel.Token);

        var answer = SingleInstanceGuard.Send(name, new InstanceRequest(InstanceRequestKind.Hello, Dev), TimeSpan.FromSeconds(5));

        Assert.Equal(PeerAnswerKind.Older, answer.Kind);
        Assert.Null(answer.Running);
        Assert.True(await connected);
    }
}
