using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Xunit;

namespace Augram.Core.Tests.Gestures;

public sealed class StarterGesturesTests
{
    [Fact]
    public void TwentyStartersWithTheNamesPlan0001Lists()
    {
        var names = StarterGestures.All().Select(gesture => gesture.Name).ToArray();

        Assert.Equal(20, names.Length);
        Assert.Equal(20, names.Distinct(GestureRules.NameComparer).Count());
        Assert.Contains("Up", names);
        Assert.Contains("Down Right", names);
        Assert.Contains("Up and Back", names);
        Assert.Contains("L Down Right", names);
        Assert.Contains("C", names);
        Assert.Contains("S", names);
        Assert.Contains("Z", names);
        Assert.Contains("Circle", names);
    }

    [Fact]
    public void EveryStarterIsActiveWithOneUsableSampleInsideTheHundredPixelBox()
    {
        foreach (var gesture in StarterGestures.All())
        {
            Assert.True(gesture.IsActive);
            var sample = Assert.Single(gesture.Samples);
            Assert.True(GestureRules.HasUsableSample(gesture), gesture.Name);
            Assert.All(sample, point => Assert.InRange(point.X, 0, 100));
            Assert.All(sample, point => Assert.InRange(point.Y, 0, 100));
        }
    }

    [Fact]
    public void IdsAreStableAcrossCallsAndDistinct()
    {
        var first = StarterGestures.All().Select(gesture => gesture.Id).ToArray();
        var second = StarterGestures.All().Select(gesture => gesture.Id).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(first.Length, first.Distinct().Count());
        Assert.Equal(StarterGestures.IdFor("Up"), first[0]);
    }

    [Fact]
    public void TheWholeSetLoadsIntoALibrary()
    {
        var library = new GestureLibrary(StarterGestures.All());

        Assert.Equal(20, library.All.Count);
    }

    [Fact]
    public void EachStarterIsRecognisedAsItselfAboveTheDefaultThreshold()
    {
        var starters = StarterGestures.All();
        var matcher = new GestureMatcher();

        foreach (var gesture in starters)
        {
            var match = matcher.Match(gesture.Samples[0], starters, RecognitionOptions.Default);

            Assert.NotNull(match);
            Assert.True(match.GestureId == gesture.Id, $"{gesture.Name} was recognised as {match.Name} ({match.Score:F1})");
        }
    }
}
