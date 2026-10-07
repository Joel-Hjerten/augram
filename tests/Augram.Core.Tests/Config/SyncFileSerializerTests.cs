using System.Text.Json.Nodes;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Config;

public sealed class SyncFileSerializerTests
{
    private static readonly Guid Machine = new("6d1e7f3a-0000-4000-8000-0000000000aa");
    private static readonly Guid Other = new("6d1e7f3a-0000-4000-8000-0000000000bb");

    [Fact]
    public void AFileRoundTrips()
    {
        var file = SampleFile();

        var json = SyncFileSerializer.Write(file);
        var back = SyncFileSerializer.Read(json, FakeStepType.Registry, notice: null);

        Assert.Equal(file.MachineId, back.MachineId);
        Assert.Equal(file.MachineName, back.MachineName);
        Assert.Equal(file.WrittenAt, back.WrittenAt);
        Assert.Equal(file.Revision, back.Revision);
        var entry = Assert.Single(back.Merged);
        Assert.Equal((Other, file.Merged[0].Revision), (entry.MachineId, entry.Revision));
        Assert.Equal(["command:x"], entry.Except);
        Assert.Equal(["command:x"], entry.Pending);
        Assert.Equal(json, SyncFileSerializer.Write(back));
        Assert.Same(back.Merged[0], back.MergedFrom(Other));
        Assert.Null(back.MergedFrom(Machine));
    }

    [Fact]
    public void GesturesAndMappingAreWrittenExactlyAsTheConfigFileWritesThem()
    {
        var file = SampleFile();
        var config = JsonNode.Parse(ConfigSerializer.Write(new ConfigDocument { Gestures = file.Gestures, Mapping = file.Mapping }))!;

        var sync = JsonNode.Parse(SyncFileSerializer.Write(file))!;

        Assert.True(JsonNode.DeepEquals(config["gestures"], sync["gestures"]));
        Assert.True(JsonNode.DeepEquals(config["mapping"], sync["mapping"]));
        Assert.Equal(1, (int)sync["schemaVersion"]!);
        Assert.Equal("PC-WORK", (string)sync["machine"]!["name"]!);
    }

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("{ \"schemaVersion\": 99, \"machine\": {} }", "newer Augram")]
    [InlineData("{ \"schemaVersion\": 1 }", "'machine'")]
    [InlineData("{ \"schemaVersion\": 1, \"machine\": { \"id\": \"6d1e7f3a-0000-4000-8000-0000000000aa\", \"name\": \"PC\", \"writtenAt\": \"yesterday\", \"revision\": \"6d1e7f3a-0000-4000-8000-0000000000bb\" } }", "ISO 8601")]
    public void ABrokenFileIsAnErrorLineNotAnException(string json, string part)
    {
        Assert.False(SyncFileSerializer.TryRead(json, FakeStepType.Registry, notice: null, out var file, out var error));

        Assert.Null(file);
        Assert.Contains(part, error, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatBreaksARuleIsAnErrorLine()
    {
        var file = SampleFile() with { Gestures = [SyncSamples.NewGesture("Twin"), SyncSamples.NewGesture("twin")] };

        Assert.False(SyncFileSerializer.TryRead(SyncFileSerializer.Write(file), FakeStepType.Registry, notice: null, out _, out var error));

        Assert.Contains("breaks a rule", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AStepOfAnUnknownTypeIsDroppedWithANotice()
    {
        var notices = new List<string>();

        Assert.True(SyncFileSerializer.TryRead(SyncFileSerializer.Write(SampleFile()), new Core.Steps.StepRegistry([]), notices.Add, out var file, out _));

        Assert.NotEmpty(notices);
        Assert.All(file.Mapping.AllCommands(), pair => Assert.Empty(pair.Command.Steps));
    }

    private static SyncFile SampleFile()
    {
        var (gestures, mapping) = SyncSamples.Setup();
        return new SyncFile(1, Machine, "PC-WORK", new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.FromHours(2)), gestures, MappingRules.ValidDocument(mapping))
        {
            Revision = Guid.NewGuid(),
            Merged = [new SyncAcknowledgement(Other, Guid.NewGuid(), ["command:x"], ["command:x"])],
        };
    }
}
