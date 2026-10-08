using Augram.Core.Abstractions;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The rate type the Display steps store: three decimals, Windows' whole-hertz rule, the 0.2 % "same timing" test, and its text.</summary>
public sealed class RefreshRateTests
{
    [Theory]
    [InlineData(119.88, 119880, "119.88 Hz")]
    [InlineData(120.0, 120000, "120 Hz")]
    [InlineData(23.976, 23976, "23.976 Hz")]
    [InlineData(59.94005994, 59940, "59.94 Hz")]
    [InlineData(119.8801198, 119880, "119.88 Hz")]
    [InlineData(1000.0, 1000000, "1000 Hz")]
    public void FromHertzKeepsThreeDecimals(double hertz, int millihertz, string text)
    {
        var rate = RefreshRate.FromHertz(hertz);

        Assert.Equal(millihertz, rate.Millihertz);
        Assert.Equal(text, rate.ToString());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-60.0)]
    [InlineData(1000.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FromHertzGivesUnknownForWhatNoDisplayRunsAt(double hertz)
    {
        var rate = RefreshRate.FromHertz(hertz);

        Assert.False(rate.IsKnown);
        Assert.Equal(RefreshRate.Unknown, rate);
        Assert.Equal("rate not reported", rate.ToString());
    }

    [Theory]
    [InlineData(23, 23976)]
    [InlineData(24, 24000)]
    [InlineData(25, 25000)]
    [InlineData(29, 29970)]
    [InlineData(30, 30000)]
    [InlineData(47, 47952)]
    [InlineData(50, 50000)]
    [InlineData(59, 59940)]
    [InlineData(60, 60000)]
    [InlineData(100, 100000)]
    [InlineData(119, 119880)]
    [InlineData(120, 120000)]
    [InlineData(143, 143000)]
    [InlineData(144, 144000)]
    [InlineData(239, 239760)]
    public void FromLegacyHertzIsWindowsOwnRule(int legacy, int millihertz)
    {
        Assert.Equal(millihertz, RefreshRate.FromLegacyHertz(legacy).Millihertz);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    [InlineData(5000)]
    public void FromLegacyHertzTreatsTheHardwareDefaultAsUnknown(int legacy)
    {
        Assert.False(RefreshRate.FromLegacyHertz(legacy).IsKnown);
    }

    [Theory]
    [InlineData(120.0, 119.88, true)]
    [InlineData(119.88, 120.0, true)]
    [InlineData(60.0, 59.94, true)]
    [InlineData(24.0, 23.976, true)]
    [InlineData(60.0, 59.95, true)]
    [InlineData(60.0, 60.0, true)]
    [InlineData(24.0, 25.0, false)]
    [InlineData(120.0, 100.0, false)]
    [InlineData(30.0, 29.0, false)]
    public void IsNearPairsARateWithItsTelevisionVariantOnly(double a, double b, bool near)
    {
        Assert.Equal(near, RefreshRate.FromHertz(a).IsNear(RefreshRate.FromHertz(b)));
    }

    [Fact]
    public void UnknownIsNearNothing()
    {
        Assert.False(RefreshRate.Unknown.IsNear(RefreshRate.Unknown));
        Assert.False(RefreshRate.Unknown.IsNear(RefreshRate.FromHertz(60.0)));
        Assert.False(RefreshRate.FromHertz(60.0).IsNear(RefreshRate.Unknown));
    }

    [Fact]
    public void HertzAndTextDropTrailingZeros()
    {
        Assert.Equal(119.88m, RefreshRate.FromHertz(119.88).Hertz);
        Assert.Equal("120", RefreshRate.FromHertz(120.0).Text);
        Assert.Equal("23.976", RefreshRate.FromLegacyHertz(23).Text);
    }

    [Fact]
    public void ModeAndResolutionTextReadLikeTheSummaries()
    {
        Assert.Equal("1920×1080", new DisplayResolution(1920, 1080).ToString());
        Assert.Equal("1920×1080 at 119.88 Hz", new VideoMode(new DisplayResolution(1920, 1080), RefreshRate.FromLegacyHertz(119)).ToString());
        Assert.Equal("1440×900", new VideoMode(new DisplayResolution(1440, 900), RefreshRate.Unknown).ToString());
    }
}
