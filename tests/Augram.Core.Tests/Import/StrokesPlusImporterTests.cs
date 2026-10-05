using Augram.Core.Gestures;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Reads the synthetic fixture (sample-config.json, UTF-8 with BOM) and checks every mapping and warning path.</summary>
public sealed class StrokesPlusImporterTests
{
    private static readonly ImportResult Result = ReadFixture();

    private static ImportResult ReadFixture()
    {
        using var stream = File.OpenRead(FixturePath.StrokesPlusNet("sample-config.json"));
        return StrokesPlusImporter.ReadGestures(stream);
    }

    private static Gesture Find(string name) => Result.Gestures.Single(gesture => gesture.Name == name);

    [Fact]
    public void FixtureStartsWithBomAndStillParses()
    {
        var bytes = File.ReadAllBytes(FixturePath.StrokesPlusNet("sample-config.json"));

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.NotEmpty(Result.Gestures);
    }

    [Fact]
    public void ImportsEveryUsableGestureInSourceOrder()
    {
        Assert.Equal(
            [
                "Synthetic Up Stock", "Synthetic L Shape", "Synthetic Dot", "Synthetic Twin", "Synthetic Twin (2)",
                "Synthetic Sleeper", "Synthetic Zigzag", "Synthetic Circle", "Synthetic Padded", "Synthetic Check",
            ],
            Result.Gestures.Select(gesture => gesture.Name));
    }

    [Fact]
    public void EveryGestureGetsAFreshDistinctId()
    {
        var ids = Result.Gestures.Select(gesture => gesture.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(default, ids);
    }

    [Fact]
    public void SamplesAreOrderedByOrderNotByPosition()
    {
        var samples = Find("Synthetic L Shape").Samples;

        Assert.Equal(2, samples.Count);
        Assert.Equal(4, samples[0].Count);
        Assert.Equal(new GesturePoint(0, 0), samples[0][0]);
        Assert.Equal(3, samples[1].Count);
        Assert.Equal(new GesturePoint(10, 0), samples[1][0]);
    }

    [Fact]
    public void PointsAreCopiedVerbatimIncludingFractions()
    {
        var sample = Assert.Single(Find("Synthetic Zigzag").Samples);

        Assert.Equal(
            [new(0, 0), new(50, 100), new(100, 0), new(150, 100), new(200.5, 0)],
            sample.ToArray());
    }

    [Fact]
    public void ActiveFlagIsCarriedOver()
    {
        Assert.False(Find("Synthetic Sleeper").IsActive);
        Assert.True(Find("Synthetic Zigzag").IsActive);
    }

    [Fact]
    public void NameIsTrimmed()
    {
        Assert.Contains(Result.Gestures, gesture => gesture.Name == "Synthetic Padded");
    }

    [Fact]
    public void DuplicateNameIsRenamedWithWarning()
    {
        var second = Find("Synthetic Twin (2)");

        Assert.Equal(new GesturePoint(200, 0), second.Samples[0][0]);
        Assert.Contains(Result.Warnings, warning =>
            warning.Severity == ImportSeverity.Warning
            && warning.Item == "Synthetic Twin"
            && warning.Message.Contains("Synthetic Twin (2)", StringComparison.Ordinal));
    }

    [Fact]
    public void DegenerateSampleIsDroppedButGestureSurvives()
    {
        var dot = Find("Synthetic Dot");

        Assert.Single(dot.Samples);
        Assert.Equal(3, dot.Samples[0].Count);
        Assert.Contains(Result.Warnings, warning =>
            warning.Severity == ImportSeverity.Warning && warning.Item == "Synthetic Dot" && warning.Message.Contains("Sample 1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Synthetic Empty")]
    [InlineData("Synthetic Same Point Twice")]
    public void GestureWithNoUsableSampleIsSkippedWithWarning(string name)
    {
        Assert.DoesNotContain(Result.Gestures, gesture => gesture.Name == name);
        Assert.Contains(Result.Warnings, warning =>
            warning.Severity == ImportSeverity.Warning && warning.Item == name && warning.Message.Contains("skipped", StringComparison.Ordinal));
    }

    [Fact]
    public void StockTwoPointGestureIsReportedAsInfoOnly()
    {
        var infos = Result.Warnings.Where(warning => warning.Severity == ImportSeverity.Info).ToList();

        var info = Assert.Single(infos);
        Assert.Equal("Synthetic Up Stock", info.Item);
        Assert.Equal(2, Find("Synthetic Up Stock").Samples[0].Count);
    }

    [Fact]
    public void StatsCountTheSourceNotTheImport()
    {
        Assert.Equal(new SourceStats(GestureCount: 12, SampleCount: 13, ActionCount: 3, ApplicationCount: 1), Result.Stats);
    }
}
