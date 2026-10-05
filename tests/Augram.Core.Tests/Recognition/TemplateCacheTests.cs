using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

public sealed class TemplateCacheTests
{
    private readonly TemplateCache _cache = new();
    private readonly GestureId _id = GestureId.New();

    [Fact]
    public void SameSampleAndPrecisionReturnsTheCachedArray()
    {
        var sample = new GestureSample(StockFlicks.Template("Up"));

        var first = _cache.GetAngles(_id, 0, sample, 100);
        var second = _cache.GetAngles(_id, 0, sample, 100);

        Assert.Same(first, second);
        Assert.Equal(99, first.Length);
    }

    [Fact]
    public void DifferentPrecisionIsComputedSeparately()
    {
        var sample = new GestureSample(StockFlicks.Template("Up"));

        Assert.Equal(99, _cache.GetAngles(_id, 0, sample, 100).Length);
        Assert.Equal(9, _cache.GetAngles(_id, 0, sample, 10).Length);
    }

    [Fact]
    public void RetrainedSampleUnderTheSameKeyIsRecomputed()
    {
        var up = new GestureSample(StockFlicks.Template("Up"));
        var down = new GestureSample(StockFlicks.Template("Down"));

        var upAngles = _cache.GetAngles(_id, 0, up, 100);
        var downAngles = _cache.GetAngles(_id, 0, down, 100);

        Assert.NotSame(upAngles, downAngles);
        Assert.Equal(-Math.PI / 2, upAngles[0], precision: 12);
        Assert.Equal(Math.PI / 2, downAngles[0], precision: 12);
    }

    [Fact]
    public void ClearDropsEntries()
    {
        var sample = new GestureSample(StockFlicks.Template("Up"));
        var first = _cache.GetAngles(_id, 0, sample, 100);

        _cache.Clear();

        Assert.NotSame(first, _cache.GetAngles(_id, 0, sample, 100));
    }
}
