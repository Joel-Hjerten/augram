using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// A Hotkey step's layout shortcuts on disk (schema 8, plan 0011): the step's <c>layouts</c> travels in a command's steps like
/// any parameter, byte-stable; a schema 7 file reads unchanged (no node moves) and its hotkeys have no layout shortcuts. The
/// bump exists because a schema 7 build would read the step without them and save the file back without them.
/// </summary>
public sealed class HotkeyLayoutsSerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string SwedishId = "com.apple.keylayout.Swedish-Pro";

    private static readonly StepRegistry Registry = new([HotkeyStepType.Instance]);

    private static HotkeyStep Back { get; } = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket)
    {
        Layouts = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal) { [SwedishId] = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8) },
    };

    [Fact]
    public void ACommandWithLayoutShortcuts_RoundTrips_ByteStable_AtSchemaEight()
    {
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Back", Up, new CommandStep(Back, HostPlatform.MacOS)))));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var notices = new List<string>();
        var back = ConfigSerializer.Read(json, Registry, notices.Add);

        Assert.Empty(notices);
        Assert.Equal(8, ConfigDocument.CurrentSchemaVersion);
        Assert.Contains("\"schemaVersion\": 8", json, StringComparison.Ordinal);
        Assert.Contains("\"layouts\"", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"key\": \"OpenBracket\"", StringComparison.Ordinal) < json.IndexOf("\"layouts\"", StringComparison.Ordinal), "layouts follows the main shortcut");
        Assert.Equal(Back, Assert.Single(back.Mapping.Global.Commands.Single().Steps).Step);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back.Mapping) }));
    }

    [Fact]
    public void ASchemaSevenFileReadsUnchanged_ItsHotkeysHaveNoLayoutShortcuts()
    {
        var root = JsonNode.Parse($$"""
            { "schemaVersion": 7, "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global", "commands": [
              { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Back", "trigger": null,
                "steps": [ { "type": "hotkey", "authoredOn": "MacOS", "params": { "modifiers": "Meta", "key": "OpenBracket" } } ] } ] } ] } }
            """)!.AsObject();
        var before = root["mapping"]!.DeepClone();

        var migrated = ConfigMigrations.Migrate(root, 7);
        var notices = new List<string>();
        var back = ConfigSerializer.Read(root.ToJsonString(), Registry, notices.Add);

        Assert.Equal(ConfigDocument.CurrentSchemaVersion, (int)migrated["schemaVersion"]!);
        Assert.True(JsonNode.DeepEquals(before, migrated["mapping"]));
        Assert.Empty(notices);
        var step = Assert.IsType<HotkeyStep>(Assert.Single(back.Mapping.Global.Commands.Single().Steps).Step);
        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), step);
        Assert.False(step.HasLayouts);
    }
}
