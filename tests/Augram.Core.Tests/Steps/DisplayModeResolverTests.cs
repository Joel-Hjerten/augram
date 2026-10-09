using Augram.Core.Abstractions;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The decision table of learnings 0002 §4, row by row, against a TV like Joel's and a few odd displays.</summary>
public sealed class DisplayModeResolverTests
{
    private static readonly DisplayResolution Uhd = new(3840, 2160);
    private static readonly DisplayResolution FullHd = new(1920, 1080);

    [Fact]
    public void AnOfferedRateAtTheCurrentSizeIsAChangeOfRateOnly()
    {
        var resolved = DisplayModeResolver.Resolve(FakeDisplayModes.Tv(), null, Rate(24));

        Assert.Equal(FakeDisplayModes.Mode(3840, 2160, 24), resolved.Mode);
        Assert.False(resolved.IsCurrent);
        Assert.Null(resolved.Reason);
    }

    [Fact]
    public void AnExactRateWinsOverItsTelevisionTwin()
    {
        var tv = FakeDisplayModes.Tv();

        Assert.Equal(Rate(119.88), DisplayModeResolver.Resolve(tv, null, Rate(119.88)).Mode?.Refresh);
        Assert.Equal(Rate(23.976), DisplayModeResolver.Resolve(tv, null, Rate(23.976)).Mode?.Refresh);
        Assert.Equal(Rate(60), DisplayModeResolver.Resolve(tv, null, Rate(60)).Mode?.Refresh);
    }

    [Theory]
    [InlineData(120.0, 119, 119.88)]
    [InlineData(119.88, 120, 120.0)]
    [InlineData(60.0, 59, 59.94)]
    [InlineData(24.0, 23, 23.976)]
    public void ARateTheDisplayLacksRunsAtItsTwinWhenThatIsOffered(double stored, int onlyLegacy, double runs)
    {
        var tv = FakeDisplayModes.Tv(legacyRates: [50, onlyLegacy], current: FakeDisplayModes.Mode(3840, 2160, 50));

        Assert.Equal(Rate(runs), DisplayModeResolver.Resolve(tv, null, Rate(stored)).Mode?.Refresh);
    }

    [Fact]
    public void ARateNotOfferedAtTheSizeIsUnsupportedAndTheReasonListsTheRates()
    {
        var tv = FakeDisplayModes.Tv(legacyRates: [24, 60, 119, 120]);

        var resolved = DisplayModeResolver.Resolve(tv, null, Rate(144));

        Assert.Null(resolved.Mode);
        Assert.Equal("SONY TV has no 144 Hz at 3840×2160; it offers 120, 119.88, 60, 24 Hz", resolved.Reason);
    }

    [Fact]
    public void AResolutionNotOfferedIsUnsupportedAndTheReasonListsSizesLargestFirst()
    {
        var resolved = DisplayModeResolver.Resolve(FakeDisplayModes.Tv(), new DisplayResolution(2560, 1080), null);

        Assert.Null(resolved.Mode);
        Assert.Equal("SONY TV has no 2560×1080; it offers 3840×2160, 2560×1440, 1920×1080", resolved.Reason);
    }

    [Fact]
    public void ALongListOfResolutionsIsCutWithACount()
    {
        var sizes = Enumerable.Range(1, 11).Select(i => new DisplayResolution(640 + (i * 100), 480)).ToList();
        var display = FakeDisplayModes.Tv(sizes: sizes, legacyRates: [60]);

        var reason = DisplayModeResolver.Resolve(display, new DisplayResolution(10, 10), null).Reason;

        Assert.EndsWith("1040×480 and 3 more", reason, StringComparison.Ordinal);
        Assert.StartsWith("SONY TV has no 10×10; it offers 1740×480, 1640×480", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoRefreshKeepsTheCurrentRateWhenTheNewSizeHasIt()
    {
        var resolved = DisplayModeResolver.Resolve(FakeDisplayModes.Tv(), FullHd, null);

        Assert.Equal(FakeDisplayModes.Mode(1920, 1080, 120), resolved.Mode);
    }

    [Fact]
    public void AutoRefreshTakesTheCurrentRatesTwinWhenOnlyThatIsThere()
    {
        var tv = FakeDisplayModes.Tv(sizes: [Uhd], legacyRates: [24, 60, 120]) with
        {
            Modes = [FakeDisplayModes.Mode(3840, 2160, 120), FakeDisplayModes.Mode(1920, 1080, 119.88), FakeDisplayModes.Mode(1920, 1080, 60)],
        };

        Assert.Equal(Rate(119.88), DisplayModeResolver.Resolve(tv, FullHd, null).Mode?.Refresh);
    }

    [Theory]
    [InlineData(120.0, new[] { 24, 25, 30, 50, 60 }, 60.0)]
    [InlineData(23.976, new[] { 30, 50, 60 }, 30.0)]
    [InlineData(100.0, new[] { 60, 120 }, 120.0)]
    [InlineData(90.0, new[] { 60, 120 }, 60.0)]
    public void AutoRefreshTakesTheClosestOfferedRateTheLowerOnATie(double current, int[] offered, double expected)
    {
        var display = FakeDisplayModes.Tv(sizes: [Uhd], current: FakeDisplayModes.Mode(3840, 2160, current)) with
        {
            Modes = [FakeDisplayModes.Mode(3840, 2160, current), .. offered.Select(rate => FakeDisplayModes.Mode(1920, 1080, rate))],
        };

        Assert.Equal(Rate(expected), DisplayModeResolver.Resolve(display, FullHd, null).Mode?.Refresh);
    }

    [Fact]
    public void AutoRefreshWithAnUnknownCurrentRateTakesTheHighest()
    {
        var display = FakeDisplayModes.Tv(current: new VideoMode(Uhd, RefreshRate.Unknown)) with
        {
            Modes = [new VideoMode(Uhd, RefreshRate.Unknown), FakeDisplayModes.Mode(1920, 1080, 60), FakeDisplayModes.Mode(1920, 1080, 120)],
        };

        Assert.Equal(Rate(120), DisplayModeResolver.Resolve(display, FullHd, null).Mode?.Refresh);
    }

    [Fact]
    public void ADisplayThatReportsNoRatesStillChangesResolutionButNotARate()
    {
        var builtIn = new DisplayInfo(
            "1",
            "Built-in display",
            new DisplayBounds(0, 0, 1512, 982),
            true,
            new VideoMode(new DisplayResolution(1512, 982), RefreshRate.Unknown),
            [new VideoMode(new DisplayResolution(1512, 982), RefreshRate.Unknown), new VideoMode(new DisplayResolution(1800, 1169), RefreshRate.Unknown)]);

        Assert.Equal(new VideoMode(new DisplayResolution(1800, 1169), RefreshRate.Unknown), DisplayModeResolver.Resolve(builtIn, new DisplayResolution(1800, 1169), null).Mode);

        var rate = DisplayModeResolver.Resolve(builtIn, null, Rate(60));
        Assert.Null(rate.Mode);
        Assert.Equal("Built-in display has no 60 Hz at 1512×982; it reports no refresh rates there", rate.Reason);
    }

    [Fact]
    public void TheCurrentModeIsResolvedAsCurrent()
    {
        var tv = FakeDisplayModes.Tv();

        var resolved = DisplayModeResolver.Resolve(tv, Uhd, Rate(120));

        Assert.True(resolved.IsCurrent);
        Assert.Equal(tv.Current, resolved.Mode);
        Assert.True(DisplayModeResolver.Resolve(tv, Uhd, null).IsCurrent);
    }

    [Fact]
    public void ResolutionAndRateTogetherAreOneMode()
    {
        var resolved = DisplayModeResolver.Resolve(FakeDisplayModes.Tv(), FullHd, Rate(23.976));

        Assert.Equal(FakeDisplayModes.Mode(1920, 1080, 23.976), resolved.Mode);
        Assert.False(resolved.IsCurrent);
    }

    [Fact]
    public void AResolutionWhoseRatesLackTheTargetChangesNothing()
    {
        // 4096×2160 at 24 and 60 only: asking it for 120 Hz must not switch the size alone.
        var display = FakeDisplayModes.Tv() with { Modes = [.. FakeDisplayModes.Tv().Modes, FakeDisplayModes.Mode(4096, 2160, 24), FakeDisplayModes.Mode(4096, 2160, 60)] };

        var resolved = DisplayModeResolver.Resolve(display, new DisplayResolution(4096, 2160), Rate(120));

        Assert.Null(resolved.Mode);
        Assert.Equal("SONY TV has no 120 Hz at 4096×2160; it offers 60, 24 Hz", resolved.Reason);
    }

    [Fact]
    public void HighestAvailableTakesTheTopRateAtTheTargetSize_IgnoringAStoredRate()
    {
        var display = FakeDisplayModes.Tv();
        var topAtFullHd = display.Modes.Where(mode => mode.Resolution == FullHd).Max(mode => mode.Refresh.Millihertz);

        var resolved = DisplayModeResolver.Resolve(display, FullHd, Rate(24), highest: true);

        Assert.Equal(new VideoMode(FullHd, new RefreshRate(topAtFullHd)), resolved.Mode);
    }

    private static RefreshRate Rate(double hertz) => RefreshRate.FromHertz(hertz);
}
