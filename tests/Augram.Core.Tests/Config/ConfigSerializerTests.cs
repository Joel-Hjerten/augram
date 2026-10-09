using Augram.Core.Config;
using Augram.Core.Gestures;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class ConfigSerializerTests
{
    [Fact]
    public void RoundTripPreservesSettingsGesturesAndExactPoints()
    {
        var document = SampleDocuments.Full();

        var back = ConfigSerializer.Read(ConfigSerializer.Write(document));

        Assert.Equal(ConfigDocument.CurrentSchemaVersion, back.SchemaVersion);
        Assert.Equal(document.Settings, back.Settings);
        Assert.Equal(document.Gestures.Count, back.Gestures.Count);
        foreach (var (expected, actual) in document.Gestures.Zip(back.Gestures))
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.IsActive, actual.IsActive);
            Assert.Equal(expected.Samples.Count, actual.Samples.Count);
            foreach (var (expectedSample, actualSample) in expected.Samples.Zip(actual.Samples))
            {
                Assert.Equal(expectedSample.ToArray(), actualSample.ToArray());
            }
        }
    }

    [Fact]
    public void WriteIsIndentedCamelCaseWithStringEnumsAndCompactSamples()
    {
        var json = ConfigSerializer.Write(SampleDocuments.Full());

        Assert.StartsWith("{\n  \"schemaVersion\": 3,", json.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\"strokeButton\": \"Middle\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ignoreKey\": \"Control, Alt\"", json, StringComparison.Ordinal);
        Assert.Contains("\"noMatch\": \"ReplayClick\"", json, StringComparison.Ordinal);
        Assert.Contains("[[1,2],[3,4]]", json, StringComparison.Ordinal);
        Assert.Contains($"\"id\": \"{SampleDocuments.ThreeGestures[0].Id.Value}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAlwaysEmitsTheCurrentSchemaVersion()
    {
        var json = ConfigSerializer.Write(new ConfigDocument { SchemaVersion = 0 });

        Assert.Contains("\"schemaVersion\": 3", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"schemaVersion\": 4 }", "newer")]
    [InlineData("{ \"schemaVersion\": 99, \"gestures\": [] }", "newer")]
    [InlineData("{ \"schemaVersion\": 0 }", "not valid")]
    [InlineData("{ \"schemaVersion\": \"one\" }", "integer")]
    [InlineData("{ \"gestures\": [] }", "schemaVersion")]
    [InlineData("[]", "object")]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("{ \"schemaVersion\": 1, \"gestures\": [ { \"id\": \"x\", \"name\": \"a\", \"isActive\": true, \"samples\": [] } ] }", "could not be read")]
    [InlineData("{ \"schemaVersion\": 1, \"gestures\": [ { \"id\": \"6d1e7f3a-0000-4000-8000-000000000001\", \"isActive\": true, \"samples\": [] } ] }", "name")]
    [InlineData("{ \"schemaVersion\": 1, \"settings\": { \"trail\": { \"colour\": \"green\" } } }", "RRGGBB")]
    public void UnreadableDocumentsThrowWithAClearMessage(string json, string messagePart)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Read(json));

        Assert.Contains(messagePart, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSectionsAndMembersTakeDefaults()
    {
        var back = ConfigSerializer.Read("{ \"schemaVersion\": 1, \"settings\": { \"general\": null, \"trail\": { \"widthPx\": 9, \"extra\": 1 }, \"capture\": { \"minSegmentPx\": 4 } } }");

        Assert.Equal(9, back.Settings.Trail.WidthPx);
        Assert.Equal(TrailSettings.Default.Opacity, back.Settings.Trail.Opacity);
        Assert.Equal(TrailSettings.Default.Colour, back.Settings.Trail.Colour);
        Assert.Equal(GeneralSettings.Default, back.Settings.General);
        Assert.Equal(4, back.Settings.Capture.MinSegmentPx);
        Assert.Equal(Core.Capture.CaptureThresholds.Default.StartDistancePx, back.Settings.Capture.StartDistancePx);
        Assert.Equal(Core.Recognition.RecognitionOptions.Default, back.Settings.Recognition);
        Assert.Empty(back.Gestures);

        var bare = ConfigSerializer.Read("{ \"schemaVersion\": 1, \"settings\": null }");
        Assert.Equal(Settings.Default, bare.Settings);
    }

    [Fact]
    public void ColourIsAHexStringInTheFile()
    {
        var json = ConfigSerializer.Write(SampleDocuments.Full());

        Assert.Contains("\"colour\": \"#0A141E\"", json, StringComparison.Ordinal);
        Assert.True(RgbColor.TryParse("#00ff40", out var parsed));
        Assert.Equal(TrailSettings.Default.Colour, parsed);
        Assert.False(RgbColor.TryParse("#00ff4", out _));
    }

    [Fact]
    public void CommentsAndTrailingCommasAreToleratedWhenHandEdited()
    {
        var back = ConfigSerializer.Read("{ // hand-edited\n \"schemaVersion\": 1, \"gestures\": [], }");

        Assert.Empty(back.Gestures);
    }

    [Fact]
    public void SamplesReadFromHandWrittenPointArrays()
    {
        var back = ConfigSerializer.Read(
            "{ \"schemaVersion\": 1, \"gestures\": [ { \"id\": \"6d1e7f3a-0000-4000-8000-000000000001\", \"name\": \"Up\", \"isActive\": true, \"samples\": [ [ [0, 100], [0, 0] ] ] } ] }");

        var gesture = Assert.Single(back.Gestures);
        Assert.Equal(new GestureId(Guid.Parse("6d1e7f3a-0000-4000-8000-000000000001")), gesture.Id);
        Assert.Equal([new GesturePoint(0, 100), new GesturePoint(0, 0)], Assert.Single(gesture.Samples).ToArray());
    }

    [Fact]
    public void MigrationRefusesAVersionItHasNoStepFor()
    {
        var root = new System.Text.Json.Nodes.JsonObject { ["schemaVersion"] = 1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigMigrations.Migrate(root, 0));
        Assert.Same(root, ConfigMigrations.Migrate(root, ConfigDocument.CurrentSchemaVersion));
    }
}
