using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>Our own Scroll step notches are claimed one each; anything else simulated (a vendor tool's re-posted wheel) is not; announced notches expire.</summary>
public sealed class OwnWheelInjectionsTests
{
    [Fact]
    public void NothingAnnouncedClaimsNothing()
    {
        var own = new OwnWheelInjections(() => 0);

        Assert.False(own.TryClaim());
    }

    [Fact]
    public void EachAnnouncedNotchIsClaimedOnce()
    {
        var own = new OwnWheelInjections(() => 0);
        own.Expect(2);
        own.Expect(1);

        Assert.True(own.TryClaim());
        Assert.True(own.TryClaim());
        Assert.True(own.TryClaim());
        Assert.False(own.TryClaim());
        Assert.Equal(0, own.Pending);
    }

    [Fact]
    public void NotchesThatNeverCameBackExpire()
    {
        long now = 0;
        var own = new OwnWheelInjections(() => now);
        own.Expect(2);
        Assert.True(own.TryClaim());

        now += (long)OwnWheelInjections.Lifetime.TotalMilliseconds + 1;

        Assert.Equal(0, own.Pending);
        Assert.False(own.TryClaim());

        // A new announcement after expiry starts from zero, not from the stale one.
        own.Expect(1);
        Assert.True(own.TryClaim());
        Assert.False(own.TryClaim());
    }

    [Fact]
    public void ZeroOrNegativeNotchesAnnounceNothing()
    {
        var own = new OwnWheelInjections(() => 0);
        own.Expect(0);
        own.Expect(-3);

        Assert.False(own.TryClaim());
    }
}
