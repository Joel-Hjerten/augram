using Augram.Core.Capture;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// Our own injections are claimed one each, per slot: Scroll step notches (anything else simulated, a vendor tool's re-posted
/// wheel, is not ours), each button's releases (anything else simulated is another program's release, plan 0005 decision 10),
/// and since the Mac takes other programs' input (2026-10-19) each button's presses and moves; a withdrawn announcement claims
/// nothing; announcements expire per slot.
/// </summary>
public sealed class OwnInjectionsTests
{
    [Fact]
    public void AButtonsPressSlotIsItsOwn_ApartFromItsReleaseSlot()
    {
        foreach (var button in Enum.GetValues<MouseButton>())
        {
            Assert.Equal(button + "Press", OwnInjections.PressSlotOf(button).ToString());
            Assert.NotEqual(OwnInjections.ReleaseSlotOf(button), OwnInjections.PressSlotOf(button));
        }
    }

    [Fact]
    public void AnAnnouncedPressIsClaimedOnce_ForItsOwnButtonOnly_NeverAsARelease()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(OwnInjections.PressSlotOf(MouseButton.Left));

        Assert.False(own.TryClaim(OwnInjections.ReleaseSlotOf(MouseButton.Left)));
        Assert.False(own.TryClaim(OwnInjections.PressSlotOf(MouseButton.Right)));
        Assert.True(own.TryClaim(OwnInjections.PressSlotOf(MouseButton.Left)));
        Assert.False(own.TryClaim(OwnInjections.PressSlotOf(MouseButton.Left)));
    }

    [Fact]
    public void AnAnnouncedMoveIsClaimedOnce_AndExpiresLikeTheRest()
    {
        long now = 0;
        var own = new OwnInjections(() => now);
        own.Expect(InjectionSlot.Move, 2);

        Assert.True(own.TryClaim(InjectionSlot.Move));
        now += (long)OwnInjections.Lifetime.TotalMilliseconds + 1;

        Assert.Equal(0, own.Pending(InjectionSlot.Move));
        Assert.False(own.TryClaim(InjectionSlot.Move));
    }

    [Theory]
    [InlineData(InjectionSlot.Wheel)]
    [InlineData(InjectionSlot.Right)]
    public void NothingAnnouncedClaimsNothing(InjectionSlot slot)
    {
        var own = new OwnInjections(() => 0);

        Assert.False(own.TryClaim(slot));
    }

    [Fact]
    public void EachAnnouncedNotchIsClaimedOnce()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(InjectionSlot.Wheel, 2);
        own.Expect(InjectionSlot.Wheel, 1);

        Assert.True(own.TryClaim(InjectionSlot.Wheel));
        Assert.True(own.TryClaim(InjectionSlot.Wheel));
        Assert.True(own.TryClaim(InjectionSlot.Wheel));
        Assert.False(own.TryClaim(InjectionSlot.Wheel));
        Assert.Equal(0, own.Pending(InjectionSlot.Wheel));
    }

    [Fact]
    public void EachAnnouncedReleaseIsClaimedOnce_ForItsOwnSlotOnly()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(InjectionSlot.Right);
        own.Expect(InjectionSlot.Right);
        own.Expect(InjectionSlot.Middle);
        own.Expect(InjectionSlot.Wheel, 1);

        Assert.False(own.TryClaim(InjectionSlot.Left));
        Assert.True(own.TryClaim(InjectionSlot.Right));
        Assert.True(own.TryClaim(InjectionSlot.Right));
        Assert.False(own.TryClaim(InjectionSlot.Right));
        Assert.Equal(1, own.Pending(InjectionSlot.Middle));
        Assert.True(own.TryClaim(InjectionSlot.Middle));
        Assert.Equal(1, own.Pending(InjectionSlot.Wheel));
    }

    [Fact]
    public void AButtonsSlotIsItsOwnNumber()
    {
        foreach (var button in Enum.GetValues<MouseButton>())
        {
            Assert.Equal(button.ToString(), OwnInjections.ReleaseSlotOf(button).ToString());
        }

        Assert.Equal((int)InjectionSlot.Wheel, Enum.GetValues<MouseButton>().Length);
    }

    [Fact]
    public void AWithdrawnAnnouncementClaimsNothing()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(InjectionSlot.Right);
        own.Withdraw(InjectionSlot.Right);
        own.Withdraw(InjectionSlot.Right);

        Assert.Equal(0, own.Pending(InjectionSlot.Right));
        Assert.False(own.TryClaim(InjectionSlot.Right));
    }

    [Fact]
    public void NotchesThatNeverCameBackExpire()
    {
        long now = 0;
        var own = new OwnInjections(() => now);
        own.Expect(InjectionSlot.Wheel, 2);
        Assert.True(own.TryClaim(InjectionSlot.Wheel));

        now += (long)OwnInjections.Lifetime.TotalMilliseconds + 1;

        Assert.Equal(0, own.Pending(InjectionSlot.Wheel));
        Assert.False(own.TryClaim(InjectionSlot.Wheel));

        // A new announcement after expiry starts from zero, not from the stale one.
        own.Expect(InjectionSlot.Wheel, 1);
        Assert.True(own.TryClaim(InjectionSlot.Wheel));
        Assert.False(own.TryClaim(InjectionSlot.Wheel));
    }

    [Fact]
    public void ReleasesThatNeverCameBackExpire()
    {
        long now = 0;
        var own = new OwnInjections(() => now);
        own.Expect(InjectionSlot.Right);
        own.Expect(InjectionSlot.Right);
        Assert.True(own.TryClaim(InjectionSlot.Right));

        now += (long)OwnInjections.Lifetime.TotalMilliseconds + 1;

        Assert.Equal(0, own.Pending(InjectionSlot.Right));
        Assert.False(own.TryClaim(InjectionSlot.Right));
    }

    [Fact]
    public void ANewAnnouncementAfterExpiryStartsFromOne()
    {
        long now = 0;
        var own = new OwnInjections(() => now);
        own.Expect(InjectionSlot.Left);
        own.Expect(InjectionSlot.Left);
        now += (long)OwnInjections.Lifetime.TotalMilliseconds + 1;
        own.Expect(InjectionSlot.Left);

        Assert.Equal(1, own.Pending(InjectionSlot.Left));
    }

    [Fact]
    public void SlotsExpireApart()
    {
        long now = 0;
        var own = new OwnInjections(() => now);
        own.Expect(InjectionSlot.Left);
        now += 600;
        own.Expect(InjectionSlot.Wheel, 1);
        now += 600;

        Assert.Equal(0, own.Pending(InjectionSlot.Left));
        Assert.Equal(1, own.Pending(InjectionSlot.Wheel));
    }

    [Fact]
    public void ZeroOrNegativeNotchesAnnounceNothing()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(InjectionSlot.Wheel, 0);
        own.Expect(InjectionSlot.Wheel, -3);

        Assert.False(own.TryClaim(InjectionSlot.Wheel));
    }
}
