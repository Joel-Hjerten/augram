using Augram.App.Hosting;
using Xunit;

namespace Augram.App.Tests.Hosting;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void SecondAcquireFailsAndSignalReachesTheFirst()
    {
        var name = "AugramTest-" + Guid.NewGuid().ToString("N");
        using var shown = new ManualResetEventSlim();
        using var first = SingleInstanceGuard.TryAcquire(name);
        Assert.NotNull(first);
        first.ShowRequested += (_, _) => shown.Set();

        using var second = SingleInstanceGuard.TryAcquire(name);
        Assert.Null(second);

        Assert.True(SingleInstanceGuard.SignalExisting(name, TimeSpan.FromSeconds(5)));
        Assert.True(shown.Wait(TimeSpan.FromSeconds(30)), "the first instance was not asked to show its window");
    }

    [Fact]
    public void NameIsFreeAgainAfterDispose()
    {
        var name = "AugramTest-" + Guid.NewGuid().ToString("N");
        var first = SingleInstanceGuard.TryAcquire(name);
        Assert.NotNull(first);
        first.Dispose();

        using var again = SingleInstanceGuard.TryAcquire(name);

        Assert.NotNull(again);
    }

    [Fact]
    public void SignalReportsFalseWhenNobodyListens()
    {
        var name = "AugramTest-" + Guid.NewGuid().ToString("N");

        Assert.False(SingleInstanceGuard.SignalExisting(name, TimeSpan.FromMilliseconds(200)));
    }
}
