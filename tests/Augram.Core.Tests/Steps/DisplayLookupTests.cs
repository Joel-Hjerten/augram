using Augram.Core.Abstractions;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>Which display a Display step acts on: the one under the gesture start, or the main one.</summary>
public sealed class DisplayLookupTests
{
    private static readonly DisplayInfo Left = FakeDisplayModes.Tv(@"\\.\DISPLAY2", "Left", new DisplayBounds(-1920, 0, 1920, 1080), isMain: false);
    private static readonly DisplayInfo Main = FakeDisplayModes.Tv(@"\\.\DISPLAY1", "Main", new DisplayBounds(0, 0, 3840, 2160), isMain: true);

    [Theory]
    [InlineData(100, 100, "Main")]
    [InlineData(-1, 0, "Left")]
    [InlineData(-1920, 1079, "Left")]
    [InlineData(3839, 2159, "Main")]
    public void UnderGestureIsTheDisplayContainingTheStart(int x, int y, string expected)
    {
        Assert.Equal(expected, DisplayLookup.Find([Left, Main], DisplayTarget.UnderGesture, x, y)?.Name);
    }

    [Theory]
    [InlineData(3840, 100)]
    [InlineData(-1921, 0)]
    [InlineData(0, 2160)]
    public void APointOnNoDisplayFindsNothing(int x, int y)
    {
        Assert.Null(DisplayLookup.Find([Left, Main], DisplayTarget.UnderGesture, x, y));
    }

    [Fact]
    public void MainIsTheFlaggedOneWhereverTheGestureWas()
    {
        Assert.Same(Main, DisplayLookup.Find([Left, Main], DisplayTarget.Main, -500, 500));
    }

    [Fact]
    public void WithoutAFlagMainIsTheDisplayAtTheOriginThenTheFirst()
    {
        var origin = Main with { IsMain = false };
        var elsewhere = Left with { IsMain = false, Bounds = new DisplayBounds(5000, 0, 100, 100) };

        Assert.Same(origin, DisplayLookup.Main([Left with { IsMain = false }, origin]));
        Assert.Same(elsewhere, DisplayLookup.Main([elsewhere]));
        Assert.Null(DisplayLookup.Main([]));
    }
}
