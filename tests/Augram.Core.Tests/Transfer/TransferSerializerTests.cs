using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps.Unknown;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// Reading an Augram file (plan 0003, decisions 11 and 12): the config file's version check and migrations, a file breaking
/// a rule refused whole, a step type this build lacks kept with a notice, and any Augram JSON readable, never with its sync section.
/// </summary>
public sealed class TransferSerializerTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string UpId = "4f0c5a7e-0000-4000-8000-000000000001";

    [Fact]
    public void AFileFromANewerAugram_IsRefusedWithAMessageSayingSo()
    {
        int newer = ConfigDocument.CurrentSchemaVersion + 1;
        var json = $$"""{ "schemaVersion": {{newer}}, "gestures": [] }""";

        Assert.False(TransferSerializer.TryRead(json, Registry, out var file, out var error));

        Assert.Null(file);
        Assert.Equal($"The file was written by a newer Augram (schema version {newer}); this build reads up to version {ConfigDocument.CurrentSchemaVersion}.", error);
    }

    [Fact]
    public void AFileFromAnOlderAugram_IsMigrated_AndSaysWhichVersionItWas()
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "gestures": [ { "id": "{{UpId}}", "name": "Up", "isActive": true, "samples": [ [[0,500],[0,100]] ] } ],
              "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global",
                "commands": [ { "id": "5d0c5a7e-0000-4000-8000-000000000002", "name": "Minimize", "trigger": { "gesture": "{{UpId}}" },
                  "steps": [ { "type": "fake", "params": { "text": "min" } } ] } ] } ] }
            }
            """;

        var file = TransferSerializer.Read(json, Registry);

        Assert.Equal(1, file.SchemaVersion);
        Assert.Equal("Up", file.Gestures.Single().Name);
        var minimize = file.Mapping!.Global.Commands.Single();
        Assert.Equal(Trigger.ForGesture(file.Gestures[0].Id), minimize.Trigger);
        Assert.Null(file.Settings);
    }

    [Theory]
    [InlineData("not json", "The file is not valid JSON")]
    [InlineData("[]", "The file must be a JSON object at the top level.")]
    [InlineData("""{ "Gestures": [] }""", "The file has no 'schemaVersion' field.")]
    public void SomethingThatIsNotAnAugramFile_IsOneErrorLine(string json, string start)
    {
        Assert.False(TransferSerializer.TryRead(json, Registry, out _, out var error));

        Assert.StartsWith(start, error, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileBreakingARule_IsRefusedWhole_NamingTheRule()
    {
        var json = $$"""
            {
              "schemaVersion": {{ConfigDocument.CurrentSchemaVersion}},
              "gestures": [
                { "id": "{{UpId}}", "name": "Up", "isActive": true, "samples": [ [[0,500],[0,100]] ] },
                { "id": "4f0c5a7e-0000-4000-8000-000000000009", "name": "up", "isActive": true, "samples": [ [[0,100],[0,500]] ] }
              ]
            }
            """;

        Assert.False(TransferSerializer.TryRead(json, Registry, out _, out var error));

        Assert.StartsWith("The file breaks a rule: ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AStepOfATypeThisBuildLacks_IsKeptAsIs_WithANotice()
    {
        var json = $$"""
            {
              "schemaVersion": {{ConfigDocument.CurrentSchemaVersion}},
              "gestures": [],
              "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global",
                "commands": [ { "id": "5d0c5a7e-0000-4000-8000-000000000002", "name": "Beam me up", "trigger": null,
                  "steps": [ { "type": "teleport", "params": { "to": "bridge" } } ] } ] } ] }
            }
            """;

        var file = TransferSerializer.Read(json, Registry);

        Assert.Contains("teleport", Assert.Single(file.Notices), StringComparison.Ordinal);
        var step = Assert.Single(file.Mapping!.Global.Commands.Single().Steps);
        Assert.IsType<UnknownStep>(step.Step);
        Assert.Contains("\"teleport\"", TransferSerializer.Write(file), StringComparison.Ordinal);
    }

    [Fact]
    public void TheConfigFileItself_ReadsAsAFile_WithoutThisMachinesSyncSection()
    {
        var config = SampleConfig();

        var file = TransferSerializer.Read(ConfigSerializer.Write(config), Registry);

        Assert.Equal(config.Settings with { Sync = SyncSettings.Default }, file.Settings);
        Assert.Equal(Contents(config), Contents(file.Gestures, file.Mapping));
    }

    [Fact]
    public void ASyncMachineFile_ReadsAsAFile_ItsMachineHeaderPassedOver()
    {
        var config = SampleConfig();
        var machineFile = new SyncFile(ConfigDocument.CurrentSchemaVersion, Guid.NewGuid(), "PC-HOME", DateTimeOffset.UnixEpoch, config.Gestures, config.Mapping)
        {
            Revision = Guid.NewGuid(),
        };

        var file = TransferSerializer.Read(SyncFileSerializer.Write(machineFile), Registry);

        Assert.Null(file.Settings);
        Assert.Equal(Contents(config), Contents(file.Gestures, file.Mapping));
    }
}
