using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.HoldRemaps.Support;
using Augram.Core.Tests.Mapping.Support;
using Xunit;

namespace Augram.Core.Tests.Config;

/// <summary>
/// Hold remaps on disk (F9, schema 4): a group's <c>holdRemaps</c>, a command's <c>holdRemap</c>, the input trigger and the
/// Remap step; written only when there is something to write, read leniently like categories, and an older file reads as
/// no hold remaps.
/// </summary>
public sealed class HoldRemapSerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string BlenderId = "6d1e7f3a-0000-4000-8000-0000000000b1";
    private const string SpaceId = "6d1e7f3a-0000-4000-8000-0000000000a1";

    private static readonly StepRegistry Registry = new([FakeStepType.Instance, RemapStepType.Instance]);

    private readonly List<string> _notices = [];

    [Fact]
    public void RoundTripKeepsHoldRemapsInputsAndRemapSteps_ByteStable()
    {
        var document = new ConfigDocument { Mapping = Blender.Document() };

        var json = ConfigSerializer.Write(document);
        var back = ConfigSerializer.Read(json, Registry, _notices.Add);

        Assert.Empty(_notices);
        var (blender, backBlender) = (document.Mapping.Groups[1], back.Mapping.Groups[1]);
        Assert.Equal(blender.HoldRemaps, backBlender.HoldRemaps);
        Assert.Equal(blender.Commands.Select(command => (command.HoldRemapId, command.Trigger)), backBlender.Commands.Select(command => (command.HoldRemapId, command.Trigger)));
        Assert.Equal(blender.Commands.Select(command => command.Steps[0].Step), backBlender.Commands.Select(command => command.Steps[0].Step));
        Assert.Equal(json, ConfigSerializer.Write(back));
    }

    [Fact]
    public void TheMembersAreWrittenWhereTheReadmeSays()
    {
        var space = Blender.NewSpace();
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = Blender.Document(Blender.Group(space)) }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains($"\"matcher\": {{", json, StringComparison.Ordinal);
        Assert.Contains(
            $"\"holdRemaps\": [\n          {{\n            \"id\": \"{space.Id}\",\n            \"name\": \"Space\",\n            \"holdKey\": \"Space\",\n            \"tapTimeMs\": 180,\n            \"isActive\": true\n          }}\n        ],\n        \"commands\": [",
            json,
            StringComparison.Ordinal);
        Assert.Contains($"\"isActive\": true,\n            \"holdRemap\": \"{space.Id}\",\n            \"steps\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"trigger\": {\n              \"input\": {\n                \"buttons\": \"Left, Right\"\n              }\n            }", json, StringComparison.Ordinal);
        Assert.Contains("\"input\": {\n                \"wheel\": \"Down\"\n              }", json, StringComparison.Ordinal);
        Assert.Contains("\"input\": {\n                \"key\": \"W\"\n              }", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"remap\",", json, StringComparison.Ordinal);
        Assert.Contains("\"params\": {\n                  \"output\": \"Button\",\n                  \"button\": \"Middle\",\n                  \"modifiers\": \"Shift\"\n                }", json, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsWrittenWithoutHoldRemaps_AndAHoldRemapsUseOnOnlyWhenSet()
    {
        var plain = ConfigSerializer.Write(new ConfigDocument
        {
            Mapping = MappingRules.ValidDocument(MappingFixtures.Document(
                MappingFixtures.NewGlobal(MappingFixtures.NewCommand("Close", MappingFixtures.Up)),
                MappingFixtures.NewGroup("Chrome", commands: [MappingFixtures.NewCommand("Close tab", Trigger.ForWheel(WheelDirection.Up))]))),
        });
        var windowsOnly = ConfigSerializer.Write(new ConfigDocument { Mapping = Blender.Document(Blender.Group(Blender.NewSpace() with { UseOn = PlatformSet.Windows })) });

        Assert.DoesNotContain("holdRemap", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input\"", plain, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(windowsOnly, "\"useOn\""));
        Assert.Equal(PlatformSet.Windows, ConfigSerializer.Read(windowsOnly, Registry, null).Mapping.Groups[1].HoldRemaps[0].UseOn);
    }

    [Fact]
    public void AFileWithoutTheMembersReadsAsNoHoldRemaps_SchemaThreeMigrates()
    {
        var back = ConfigSerializer.Read($$"""
            { "schemaVersion": 3, "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global", "commands": [] },
              { "id": "{{BlenderId}}", "name": "Blender", "matcher": { "processNames": ["blender.exe"] },
                "commands": [ { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Close", "trigger": null } ] } ] } }
            """);

        Assert.Equal(ConfigDocument.CurrentSchemaVersion, back.SchemaVersion);
        Assert.Empty(back.Mapping.Groups[1].HoldRemaps);
        Assert.Null(back.Mapping.Groups[1].Commands[0].HoldRemapId);
    }

    [Fact]
    public void MissingHoldRemapMembersTakeTheirDefaults()
    {
        var back = Read($$"""[ { "id": "{{SpaceId}}", "holdKey": "space" } ]""", $"\"holdRemap\": \"{SpaceId}\"");

        var holdRemap = new MappingStore(back).Current.Groups[1].HoldRemaps.Single();
        Assert.Equal(("Space", KeyCode.Space, 180, true, PlatformSet.All), (holdRemap.Name, holdRemap.HoldKey, holdRemap.TapTimeMs, holdRemap.IsActive, holdRemap.UseOn));
        Assert.Empty(_notices);
    }

    [Fact]
    public void ABadHoldRemapIsDroppedWithANotice_ItsCommandsLoadAsOrdinaryWithoutTheirInput()
    {
        var back = Read(
            $$"""
            [ "Space", { "name": "No id" }, { "id": "{{SpaceId}}", "holdKey": "Hyper" }, { "id": "6d1e7f3a-0000-4000-8000-0000000000a2", "tapTimeMs": "fast" },
              { "id": "6d1e7f3a-0000-4000-8000-0000000000a3", "name": "Kept", "holdKey": "S", "tapTimeMs": 150, "isActive": false } ]
            """,
            $"\"holdRemap\": \"{SpaceId}\"");

        Assert.Equal("Kept", Assert.Single(back.Groups[1].HoldRemaps).Name);
        Assert.Equal(4, _notices.Count);
        Assert.Equal("Hold remap 1 of app group 'Blender' dropped: it is not a JSON object.", _notices[0]);
        Assert.Equal("Hold remap 2 of app group 'Blender' dropped: 'id' is missing or not a Guid string.", _notices[1]);
        Assert.StartsWith("Hold remap 3 of app group 'Blender' dropped: 'holdKey' of the hold remap must be one of None, A, B,", _notices[2], StringComparison.Ordinal);
        Assert.Equal("Hold remap 4 of app group 'Blender' dropped: 'tapTimeMs' of the hold remap must be an integer.", _notices[3]);
        var command = new MappingStore(back).Current.Groups[1].Commands.Single();
        Assert.Null(command.HoldRemapId);
        Assert.Same(Trigger.None, command.Trigger);
    }

    [Theory]
    [InlineData("\"holdRemap\": 7")]
    [InlineData("\"holdRemap\": \"space\"")]
    public void AHoldRemapReferenceThatIsNotAGuidReadsAsNoneWithANotice(string member)
    {
        var back = Read($$"""[ { "id": "{{SpaceId}}", "name": "Space", "holdKey": "Space" } ]""", member, trigger: "null");

        Assert.Null(back.Groups[1].Commands[0].HoldRemapId);
        Assert.Equal(["The hold remap of command 'Orbit' in 'Blender' dropped: 'holdRemap' must be a Guid string; the command is an ordinary one."], _notices);
    }

    [Theory]
    [InlineData("""{ "input": "Left" }""", "'input' of 'trigger' of command 'Orbit' in 'Blender' must be a JSON object.")]
    [InlineData("""{ "input": { } }""", "'input' of 'trigger' of command 'Orbit' in 'Blender' must be { \"buttons\": \"Left, Right\" }, { \"wheel\": \"Up\" | \"Down\" } or { \"key\": \"W\" }.")]
    [InlineData("""{ "input": { "key": 4 } }""", "'key' of 'input' of 'trigger' of command 'Orbit' in 'Blender' must be a string.")]
    [InlineData("""{ "input": { "buttons": "Left, Thumb" } }""", "'buttons' of 'input' of 'trigger' of command 'Orbit' in 'Blender' must be a comma-separated list of None, Stroke, Left, Middle, Right, X1, X2.")]
    public void AnInputOfTheWrongShapeIsAFormatErrorNamingTheMember(string trigger, string message)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => Read($$"""[ { "id": "{{SpaceId}}", "holdKey": "Space" } ]""", $"\"holdRemap\": \"{SpaceId}\"", trigger));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void ABrokenSavedHoldRemapCostsOnlyItself_TheSessionLoadsTheRest()
    {
        var space = Blender.NewSpace();
        var shift = HoldRemap.For(KeyCode.LeftShift);
        var group = Blender.Group(space);
        var raw = new MappingDocument([MappingFixtures.NewGlobal(), group with { HoldRemaps = [space, shift] }], []);
        var notices = new List<string>();

        using var session = new ConfigSession(new InMemoryConfigStore(new ConfigDocument { Mapping = raw }), new ManualScheduler().Schedule, notices.Add);

        var loaded = session.Mapping.Current.Groups[1];
        Assert.Equal(space, Assert.Single(loaded.HoldRemaps));
        Assert.Equal(group.Commands.Count, loaded.Commands.Count(command => command.HoldRemapId == space.Id));
        Assert.Equal($"Hold remap 'Left Shift' ({shift.Id}) in 'Blender' skipped: Left Shift cannot be a hold key: Ctrl, Alt, Shift and Win are already held for triggers.", Assert.Single(notices));
    }

    private MappingDocument Read(string holdRemaps, string commandMember, string trigger = """{ "input": { "buttons": "Left" } }""")
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": 4, "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global", "commands": [] },
              { "id": "{{BlenderId}}", "name": "Blender", "matcher": { "processNames": ["blender.exe"] }, "holdRemaps": {{holdRemaps}},
                "commands": [ { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Orbit", "trigger": {{trigger}}, {{commandMember}},
                  "steps": [ { "type": "remap", "params": { "button": "Middle" } } ] } ] } ] } }
            """,
            Registry,
            _notices.Add).Mapping;

    private static int Occurrences(string text, string part)
        => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
