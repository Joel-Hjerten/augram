using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Display;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Display;

/// <summary>The macOS display adapter's pure rules: modes offered in points with rounded rates, and the HiDPI twin applied.</summary>
public sealed class MacModePickTests
{
    private static readonly MacNativeMode HiDpi1080 = new(1920, 1080, 3840, 2160, 60.0, Usable: true, IoModeId: 1);
    private static readonly MacNativeMode LowRes1080 = new(1920, 1080, 1920, 1080, 60.0, Usable: true, IoModeId: 2);
    private static readonly MacNativeMode LowRes1080Tv = new(1920, 1080, 1920, 1080, 23.976023976, Usable: true, IoModeId: 3);
    private static readonly MacNativeMode Native4K = new(3840, 2160, 3840, 2160, 119.88011988, Usable: true, IoModeId: 4);
    private static readonly MacNativeMode Unusable = new(640, 480, 640, 480, 60.0, Usable: false, IoModeId: 5);
    private static readonly MacNativeMode BuiltIn = new(1512, 982, 3024, 1964, 0.0, Usable: true, IoModeId: 6);

    [Fact]
    public void ModesAreOfferedInPointsWithRatesRoundedAndTwinsOnce()
    {
        var offered = MacModePick.Offered([HiDpi1080, LowRes1080, LowRes1080Tv, Native4K, Unusable, BuiltIn]);

        Assert.Equal(
            [
                Mode(1920, 1080, RefreshRate.FromHertz(60.0)),
                Mode(1920, 1080, RefreshRate.FromHertz(23.976)),
                Mode(3840, 2160, RefreshRate.FromHertz(119.88)),
                Mode(1512, 982, RefreshRate.Unknown),
            ],
            offered);
    }

    [Fact]
    public void TheHiDpiTwinIsAppliedWhenBothCarryTheMode()
    {
        Assert.Same(HiDpi1080, MacModePick.Pick([LowRes1080, HiDpi1080], Mode(1920, 1080, RefreshRate.FromHertz(60.0))));
    }

    [Fact]
    public void ARateOnlyTheLowResolutionModeHasStillApplies()
    {
        Assert.Same(LowRes1080Tv, MacModePick.Pick([HiDpi1080, LowRes1080, LowRes1080Tv], Mode(1920, 1080, RefreshRate.FromHertz(23.976))));
    }

    [Fact]
    public void AnUnknownRateMatchesAPanelThatReportsNone()
    {
        Assert.Same(BuiltIn, MacModePick.Pick([BuiltIn], Mode(1512, 982, RefreshRate.Unknown)));
    }

    [Fact]
    public void NothingIsPickedForAModeNoUsableEntryCarries()
    {
        Assert.Null(MacModePick.Pick([Unusable, HiDpi1080], Mode(640, 480, RefreshRate.FromHertz(60.0))));
        Assert.Null(MacModePick.Pick([HiDpi1080], Mode(1920, 1080, RefreshRate.FromHertz(120.0))));
    }

    private static VideoMode Mode(int width, int height, RefreshRate rate) => new(new DisplayResolution(width, height), rate);
}
