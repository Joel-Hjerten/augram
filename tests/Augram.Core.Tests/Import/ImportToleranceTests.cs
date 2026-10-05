using System.Text;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Inline documents for the edge cases: BOM, missing or empty sections, malformed input.</summary>
public sealed class ImportToleranceTests
{
    [Fact]
    public void StringWithBomIsAccepted()
    {
        var result = StrokesPlusImporter.ReadGestures("﻿{ \"Gestures\": [] }");

        Assert.Empty(result.Gestures);
    }

    [Fact]
    public void StreamWithBomIsAccepted()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat("{ \"Gestures\": [] }"u8.ToArray()).ToArray();
        using var stream = new MemoryStream(bytes);

        var result = StrokesPlusImporter.ReadGestures(stream);

        Assert.Empty(result.Gestures);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"Gestures\": null }")]
    [InlineData("{ \"Gestures\": \"not an array\" }")]
    public void MissingGesturesArrayGivesZeroGesturesAndOneWarning(string json)
    {
        var result = StrokesPlusImporter.ReadGestures(json);

        Assert.Empty(result.Gestures);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
        Assert.Equal("Gestures", warning.Item);
        Assert.Equal(new SourceStats(0, 0, null, null), result.Stats);
    }

    [Fact]
    public void EmptyGesturesArrayGivesZeroGesturesAndNoWarning()
    {
        var result = StrokesPlusImporter.ReadGestures("{ \"Gestures\": [] }");

        Assert.Empty(result.Gestures);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"Gestures\": [ }")]
    [InlineData("not json at all")]
    [InlineData("[ 1, 2, 3 ]")]
    public void MalformedInputThrowsImportFormatException(string json)
    {
        Assert.Throws<ImportFormatException>(() => StrokesPlusImporter.ReadGestures(json));
    }

    [Fact]
    public void MissingNameActiveAndPointPatternsAreTolerated()
    {
        const string json = """
            { "Gestures": [
                { "PointPatterns": [ { "Points": [ { "X": 0, "Y": 0 }, { "X": 1, "Y": 1 } ] } ] },
                { "Name": "Synthetic Null Patterns", "Active": null, "PointPatterns": null, "MultiPointPatterns": null }
            ] }
            """;

        var result = StrokesPlusImporter.ReadGestures(json);

        var gesture = Assert.Single(result.Gestures);
        Assert.Equal("Unnamed gesture 1", gesture.Name);
        Assert.True(gesture.IsActive);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Null Patterns");
    }

    [Fact]
    public void NonNumericPointsAreIgnored()
    {
        const string json = """
            { "Gestures": [ { "Name": "Synthetic Mixed", "PointPatterns": [ { "Order": 1, "Points": [
                { "X": "a", "Y": 1 }, { "X": 0, "Y": 0 }, null, { "X": 9 }, { "X": 5, "Y": 5 } ] } ] } ] }
            """;

        var result = StrokesPlusImporter.ReadGestures(json);

        var gesture = Assert.Single(result.Gestures);
        Assert.Equal(2, gesture.Samples[0].Count);
    }
}
