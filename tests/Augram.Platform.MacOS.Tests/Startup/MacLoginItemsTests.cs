using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Startup;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Startup;

/// <summary>
/// What SMAppService's status means for Augram, and which launch event is a login-item launch. The native halves
/// (<c>MacLoginItemRegistration</c>, <c>MacLaunchEvent</c>) are never called here: a test must not register a login item,
/// and the launch event exists only at a real launch (checked by Joel at a real login).
/// </summary>
public sealed class MacLoginItemsTests
{
    [Theory]
    [InlineData(0, StartupStatus.NotRegistered)]
    [InlineData(1, StartupStatus.Registered)]
    [InlineData(2, StartupStatus.NeedsApproval)]
    [InlineData(3, StartupStatus.DisabledByUser)]
    [InlineData(4, StartupStatus.NotRegistered)]
    [InlineData(-1, StartupStatus.NotRegistered)]
    public void TheStatusMaps(long status, StartupStatus expected) => Assert.Equal(expected, MacLoginItems.Map(status));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void OnlyARegisteredItemIsUnregistered(long status, bool expected) => Assert.Equal(expected, MacLoginItems.CanUnregister(status));

    [Fact]
    public void TheCodesAreTheirFourCharacters()
    {
        Assert.Equal(MacLoginItems.CoreEventClass, MacLoginItems.Code("aevt"));
        Assert.Equal(MacLoginItems.OpenApplication, MacLoginItems.Code("oapp"));
        Assert.Equal(MacLoginItems.PropData, MacLoginItems.Code("prdt"));
        Assert.Equal(MacLoginItems.LaunchedAsLogInItem, MacLoginItems.Code("lgit"));
        Assert.Equal("lgit", MacLoginItems.Text(MacLoginItems.LaunchedAsLogInItem));
        Assert.Throws<ArgumentException>(() => MacLoginItems.Code("odoc!"));
    }

    [Fact]
    public void ALoginItemLaunch_IsTheOpenApplicationEventLaunchedAsALoginItem()
    {
        var aevt = MacLoginItems.Code("aevt");
        var oapp = MacLoginItems.Code("oapp");
        var lgit = MacLoginItems.Code("lgit");

        Assert.True(MacLoginItems.IsLoginItemLaunch(aevt, oapp, lgit));
        Assert.False(MacLoginItems.IsLoginItemLaunch(aevt, oapp, 0));
        Assert.False(MacLoginItems.IsLoginItemLaunch(aevt, MacLoginItems.Code("odoc"), lgit));
        Assert.False(MacLoginItems.IsLoginItemLaunch(aevt, MacLoginItems.Code("rapp"), lgit));
        Assert.False(MacLoginItems.IsLoginItemLaunch(MacLoginItems.Code("misc"), oapp, lgit));
        Assert.False(MacLoginItems.IsLoginItemLaunch(0, 0, 0));
    }

    [Fact]
    public void TheEventReadsAsItsCodes()
    {
        Assert.Equal("aevt/oapp prdt=lgit", MacLoginItems.Describe(MacLoginItems.CoreEventClass, MacLoginItems.OpenApplication, MacLoginItems.LaunchedAsLogInItem));
        Assert.Equal("aevt/oapp prdt=-", MacLoginItems.Describe(MacLoginItems.CoreEventClass, MacLoginItems.OpenApplication, 0));
    }
}
