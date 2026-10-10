using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// Trigger combinations on disk (schema 2, 2026-10-09): a trigger's <c>hold</c>, the click trigger, an own version's
/// <c>trigger</c>; a plain trigger writes exactly as before, a schema 1 file reads unchanged, and a file an older build saved
/// over a newer one is reported.
/// </summary>
public sealed class TriggerSerializationTests : IDisposable
{
    private readonly List<string> _notices = [];
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void CombinationsClicksAndOwnTriggers_RoundTrip_InTheReadmesShape()
    {
        var shiftUp = Trigger.ForGesture(Up, TriggerHold.WithStroke(KeyModifiers.Shift, HeldButtons.Left, HoldCapture.Before));
        var rightWheel = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right | HeldButtons.X1, KeyModifiers.Control | KeyModifiers.Alt));
        var altClick = Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Alt));
        var withOwn = NewCommand("Own", Up, NewStep("x")).WithTriggerFor(HostPlatform.MacOS, Trigger.ForGesture(Down, TriggerHold.WithStroke(KeyModifiers.Meta)), new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        var unboundThere = NewCommand("Unbound there", Down, NewStep("y")).WithTriggerFor(HostPlatform.MacOS, Trigger.None, DateTimeOffset.UnixEpoch);
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(
            NewCommand("Shift up", shiftUp),
            NewCommand("Zoom", rightWheel),
            NewCommand("Alt click", altClick),
            NewCommand("Plain", Trigger.ForWheel(WheelDirection.Up)),
            withOwn,
            unboundThere)));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add).Mapping;
        Command Named(string name) => back.Global.Commands.Single(command => command.Name == name);

        Assert.Empty(_notices);
        Assert.Equal(shiftUp, Named("Shift up").Trigger);
        Assert.Equal(rightWheel, Named("Zoom").Trigger);
        Assert.Equal(altClick, Named("Alt click").Trigger);
        Assert.Equal(withOwn.OwnVersion!.Trigger, Named("Own").OwnVersion!.Trigger);
        Assert.Equal(Trigger.None, Named("Unbound there").OwnVersion!.Trigger);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = back }));
        var flat = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("\"hold\": {\n", flat, StringComparison.Ordinal);
        Assert.Contains("\"buttons\": \"Stroke, Left\",\n", flat, StringComparison.Ordinal);
        Assert.Contains("\"keys\": \"Control, Alt\"", flat, StringComparison.Ordinal);
        Assert.Contains("\"capture\": \"Before\"", flat, StringComparison.Ordinal);
        Assert.Contains("\"click\": true", flat, StringComparison.Ordinal);
        Assert.Matches("\"trigger\": \\{\\n\\s*\"wheel\": \"Up\"\\n\\s*\\}", flat);
    }

    [Fact]
    public void AnOwnVersionWithoutATrigger_UsesTheOriginal_AsBefore()
    {
        var json = $$"""
            { "schemaVersion": 1, "mapping": { "groups": [ { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "commands": [
              { "id": "00000000-0000-4000-8000-000000000002", "name": "X", "trigger": { "gesture": "{{Up.Value}}" },
                "steps": [ { "type": "fake", "authoredOn": "Windows", "params": { "text": "a" } } ],
                "ownVersion": { "platform": "MacOS", "basedOn": "", "steps": [] } } ] } ] } }
            """;

        var command = Assert.Single(ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add).Mapping.Global.Commands);

        Assert.Null(command.OwnVersion!.Trigger);
        Assert.Equal(Trigger.ForGesture(Up), command.TriggerFor(HostPlatform.MacOS));
        Assert.Equal(TriggerHold.Default, command.Trigger.Hold);
    }

    [Theory]
    [InlineData("{ \"wheel\": \"Up\", \"hold\": { \"buttons\": \"Stroke, Thumb\" } }", "'buttons' of 'hold'")]
    [InlineData("{ \"gesture\": \"00000000-0000-4000-8000-000000000009\", \"hold\": { \"keys\": \"Hyper\" } }", "'keys' of 'hold'")]
    [InlineData("{ \"wheel\": \"Up\", \"hold\": { \"capture\": \"Sometimes\" } }", "'capture' of 'hold'")]
    [InlineData("{ \"wheel\": \"Up\", \"hold\": 3 }", "'hold' of 'trigger'")]
    public void ABadHold_IsAFormatErrorThatNamesIt(string trigger, string messagePart)
    {
        var json = $$"""{ "schemaVersion": 2, "mapping": { "groups": [ { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "commands": [ { "id": "00000000-0000-4000-8000-000000000002", "name": "X", "trigger": {{trigger}} } ] } ] } }""";

        var ex = Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add));

        Assert.Contains(messagePart, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASchemaOneFile_IsMigratedAsItIs()
    {
        var root = new System.Text.Json.Nodes.JsonObject { ["schemaVersion"] = 1, ["mapping"] = new System.Text.Json.Nodes.JsonObject() };

        var migrated = ConfigMigrations.Migrate(root, 1);

        Assert.Equal(ConfigDocument.CurrentSchemaVersion, (int)migrated["schemaVersion"]!);
        Assert.NotNull(migrated["mapping"]);
    }

    /// <summary>
    /// An older build (schema 6) cannot read a schema 7 file: it loads the newest backup it can read and saves over the newer
    /// file at its first change. The newer build then says where the newer file went instead of quietly losing what it held.
    /// </summary>
    [Fact]
    public void AFileAnOlderBuildSavedOverANewerOne_IsReportedWithWhereTheNewerOneIs()
    {
        var store = new FileConfigStore(_folder.Path, _notices.Add, clock: () => new DateTime(2026, 10, 9, 12, 0, 0));
        store.Save(new ConfigDocument());
        store.Save(new ConfigDocument());
        var current = File.ReadAllText(store.Location);
        var older = current.Replace("\"schemaVersion\": 7", "\"schemaVersion\": 6", StringComparison.Ordinal);
        File.Copy(store.Location, Path.Combine(store.Backups.Folder, "augram-20261009-130000.json"));
        File.WriteAllText(store.Location, older);

        store.Load();

        var notice = Assert.Single(_notices);
        Assert.Contains("saved by an older Augram (schema 6) over a newer one (schema 7)", notice, StringComparison.Ordinal);
        Assert.Contains("augram-20261009-130000.json", notice, StringComparison.Ordinal);
    }
}
