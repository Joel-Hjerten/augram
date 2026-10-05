using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

public sealed class ConfusionCheckTests
{
    [Fact]
    public void ReportsNearIdenticalActiveGesturesOnce()
    {
        var up = TestGestures.Create("Up", StockFlicks.Template("Up"));
        var upTwin = TestGestures.Create("Up twin", StrokeBuilder.Jitter(StockFlicks.Template("Up"), 1, seed: 3));
        var down = TestGestures.Create("Down", StockFlicks.Template("Down"));

        var pairs = ConfusionCheck.Find([up, upTwin, down], RecognitionOptions.Default);

        var pair = Assert.Single(pairs);
        Assert.Equal(new HashSet<string> { "Up", "Up twin" }, new HashSet<string> { pair.FirstName, pair.SecondName });
        Assert.True(pair.Score >= ConfusionCheck.CutOff(RecognitionOptions.Default));
    }

    [Fact]
    public void StarterSetHasNoConfusablePairsAlthoughNeighbouringFlicksScoreAboveTheThreshold()
    {
        var starter = StarterGestures.All();

        Assert.Empty(ConfusionCheck.Find(starter, RecognitionOptions.Default));
        Assert.NotEmpty(ConfusionCheck.Find(starter, RecognitionOptions.Default, RecognitionOptions.Default.Threshold));
        Assert.Equal(87.5, ConfusionCheck.CutOff(RecognitionOptions.Default));
    }

    [Fact]
    public void IgnoresInactiveAndSamplelessGestures()
    {
        var up = TestGestures.Create("Up", StockFlicks.Template("Up"));
        var inactiveTwin = TestGestures.Create("Up twin", isActive: false, StockFlicks.Template("Up"));
        var placeholder = new Gesture(GestureId.New(), "Empty", IsActive: true, []);

        Assert.Empty(ConfusionCheck.Find([up, inactiveTwin, placeholder], RecognitionOptions.Default));
    }
}
