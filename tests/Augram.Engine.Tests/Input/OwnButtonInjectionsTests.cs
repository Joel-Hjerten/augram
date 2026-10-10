using Augram.Core.Capture;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// Our own button releases are claimed one each and per button; anything else simulated is another program's release (plan 0005
/// decision 10); a withdrawn announcement claims nothing; announcements expire.
/// </summary>
public sealed class OwnButtonInjectionsTests
{
    [Fact]
    public void NothingAnnouncedClaimsNothing()
    {
        var own = new OwnButtonInjections(() => 0);

        Assert.False(own.TryClaim(MouseButton.Right));
    }

    [Fact]
    public void EachAnnouncedReleaseIsClaimedOnce_ForItsOwnButtonOnly()
    {
        var own = new OwnButtonInjections(() => 0);
        own.Expect(MouseButton.Right);
        own.Expect(MouseButton.Right);
        own.Expect(MouseButton.Middle);

        Assert.False(own.TryClaim(MouseButton.Left));
        Assert.True(own.TryClaim(MouseButton.Right));
        Assert.True(own.TryClaim(MouseButton.Right));
        Assert.False(own.TryClaim(MouseButton.Right));
        Assert.Equal(1, own.Pending(MouseButton.Middle));
        Assert.True(own.TryClaim(MouseButton.Middle));
    }

    [Fact]
    public void AWithdrawnAnnouncementClaimsNothing()
    {
        var own = new OwnButtonInjections(() => 0);
        own.Expect(MouseButton.Right);
        own.Withdraw(MouseButton.Right);
        own.Withdraw(MouseButton.Right);

        Assert.Equal(0, own.Pending(MouseButton.Right));
        Assert.False(own.TryClaim(MouseButton.Right));
    }

    [Fact]
    public void ReleasesThatNeverCameBackExpire()
    {
        long now = 0;
        var own = new OwnButtonInjections(() => now);
        own.Expect(MouseButton.Right);
        own.Expect(MouseButton.Right);
        Assert.True(own.TryClaim(MouseButton.Right));

        now += (long)OwnButtonInjections.Lifetime.TotalMilliseconds + 1;

        Assert.Equal(0, own.Pending(MouseButton.Right));
        Assert.False(own.TryClaim(MouseButton.Right));
    }

    [Fact]
    public void ANewAnnouncementAfterExpiryStartsFromOne()
    {
        long now = 0;
        var own = new OwnButtonInjections(() => now);
        own.Expect(MouseButton.Left);
        own.Expect(MouseButton.Left);
        now += (long)OwnButtonInjections.Lifetime.TotalMilliseconds + 1;
        own.Expect(MouseButton.Left);

        Assert.Equal(1, own.Pending(MouseButton.Left));
    }
}
