using System.Text;
using Augram.Core.Gestures;
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

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", true)]
    [InlineData("\"false\"", true)]
    [InlineData("0", true)]
    public void OnlyAFalseActiveMakesAGestureInactive(string active, bool expected)
    {
        var result = StrokesPlusImporter.ReadGestures(
            "{ \"Gestures\": [ { \"Name\": \"Synthetic Flag\", \"Active\": " + active + ", \"PointPatterns\": [ { \"Points\": [ { \"X\": 0, \"Y\": 0 }, { \"X\": 5, \"Y\": 5 } ] } ] } ] }");

        Assert.Equal(expected, Assert.Single(result.Gestures).IsActive);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void ANameThatIsNotTextFallsBackToANumberedName(string name)
    {
        var result = StrokesPlusImporter.ReadGestures(
            "{ \"Gestures\": [ { \"Name\": " + name + ", \"PointPatterns\": [ { \"Points\": [ { \"X\": 0, \"Y\": 0 }, { \"X\": 5, \"Y\": 5 } ] } ] } ] }");

        Assert.Equal("Unnamed gesture 1", Assert.Single(result.Gestures).Name);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"9\"")]
    [InlineData("true")]
    public void AnOrderThatIsNotANumberKeepsTheSampleInItsPlace(string order)
    {
        // A sample's Order read as a number used to throw on anything else and fail the whole import.
        var result = StrokesPlusImporter.ReadGestures(
            "{ \"Gestures\": [ { \"Name\": \"Synthetic Order\", \"PointPatterns\": [ "
            + "{ \"Order\": " + order + ", \"Points\": [ { \"X\": 0, \"Y\": 0 }, { \"X\": 5, \"Y\": 5 } ] }, "
            + "{ \"Order\": 3, \"Points\": [ { \"X\": 0, \"Y\": 0 }, { \"X\": 9, \"Y\": 1 } ] } ] } ] }");

        var gesture = Assert.Single(result.Gestures);
        Assert.Equal([new GesturePoint(5, 5), new GesturePoint(9, 1)], gesture.Samples.Select(sample => sample[1]));
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
