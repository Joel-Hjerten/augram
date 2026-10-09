using Augram.Platform.MacOS.Overlay;
using Augram.Platform.MacOS.WindowSystem;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Overlay;

/// <summary>Which display the trail panel covers, and where a stroke point lands in its bottom-left layer.</summary>
public sealed class MacTrailGeometryTests
{
    // A 1512×982 main display with a 2560×1440 display to its right whose top is 200 pt higher.
    private static readonly MacRect Main = new(0, 0, 1512, 982);
    private static readonly MacRect External = new(1512, -200, 2560, 1440);
    private static readonly MacScreen[] Screens = [new(Main, Main), new(External, External)];

    [Fact]
    public void ScreenFor_TheDisplayUnderTheStrokeStart()
    {
        Assert.Equal(Main, MacTrailGeometry.ScreenFor(Screens, 700, 500));
        Assert.Equal(External, MacTrailGeometry.ScreenFor(Screens, 1512, -150));
    }

    [Fact]
    public void ScreenFor_APointOffEveryDisplayTakesTheNearest()
    {
        // Below the main display's bottom edge, which belongs to no display.
        Assert.Equal(Main, MacTrailGeometry.ScreenFor(Screens, 700, 982));
        Assert.Equal(External, MacTrailGeometry.ScreenFor(Screens, 4100, 0));
    }

    [Fact]
    public void ScreenFor_NoScreensFallsBack()
        => Assert.Equal(MacTrailGeometry.Fallback, MacTrailGeometry.ScreenFor([], 10, 10));

    [Fact]
    public void ToLayer_FlipsYInsideTheCoveredDisplay()
    {
        Assert.Equal((0.0, 982.0), MacTrailGeometry.ToLayer(0, 0, Main));
        Assert.Equal((700.0, 482.0), MacTrailGeometry.ToLayer(700, 500, Main));

        // On the external display: its top-left corner is the layer's top-left, 1440 pt up.
        Assert.Equal((0.0, 1440.0), MacTrailGeometry.ToLayer(1512, -200, External));
        Assert.Equal((100.0, 1240.0), MacTrailGeometry.ToLayer(1612, 0, External));
    }
}
