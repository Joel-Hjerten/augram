using Augram.App.Overlay;
using Augram.Core.Abstractions;
using Xunit;

namespace Augram.App.Tests.Overlay;

/// <summary>The rule that keeps a non-click-through overlay off the screen.</summary>
public sealed class OverlayStylePolicyTests
{
    [Theory]
    [InlineData(true, true, true, OverlayStyleDecision.Show)]
    [InlineData(true, false, true, OverlayStyleDecision.Show)]
    [InlineData(true, true, false, OverlayStyleDecision.Show)]
    [InlineData(true, false, false, OverlayStyleDecision.Show)]
    [InlineData(false, true, true, OverlayStyleDecision.Hide)]
    [InlineData(false, false, false, OverlayStyleDecision.Hide)]
    public void OnlyClickThroughDecidesVisibility(bool clickThrough, bool noActivate, bool toolWindow, OverlayStyleDecision expected)
    {
        var report = new OverlayStyleReport(clickThrough, noActivate, toolWindow, "0x0");

        Assert.Equal(expected, OverlayStylePolicy.Decide(report));
    }

    [Fact]
    public void MissingNoActivateOrToolWindowIsACosmeticFault_NotWhenHidden()
    {
        Assert.False(OverlayStylePolicy.HasCosmeticFault(new OverlayStyleReport(true, true, true, "0x0")));
        Assert.True(OverlayStylePolicy.HasCosmeticFault(new OverlayStyleReport(true, false, true, "0x0")));
        Assert.True(OverlayStylePolicy.HasCosmeticFault(new OverlayStyleReport(true, true, false, "0x0")));
        Assert.False(OverlayStylePolicy.HasCosmeticFault(new OverlayStyleReport(false, false, false, "0x0")));
    }

    [Fact]
    public void ThePlatformWithoutStylesIsShowable()
    {
        Assert.Equal(OverlayStyleDecision.Show, OverlayStylePolicy.Decide(OverlayStyleReport.NotApplicable));
        Assert.False(OverlayStylePolicy.HasCosmeticFault(OverlayStyleReport.NotApplicable));
    }
}
