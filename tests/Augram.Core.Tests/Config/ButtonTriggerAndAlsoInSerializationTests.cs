using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// Button triggers and a command's "Also in" on disk (schema 7, plan 0005): <c>{ "button": "Left", "hold": { "buttons": "Right" } }</c>
/// in a command and an own version alike, the Remap step on it, and <c>alsoIn</c> (Exclusions › Global entry ids) written only
/// when set; a schema 6 file reads as none of them, a bad button name is a format error, a bad <c>alsoIn</c> entry never fails a
/// load, and the session's rule-by-rule fallback keeps an "Also in" naming entries that load after the groups.
/// </summary>
public sealed class ButtonTriggerAndAlsoInSerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string MagnifierId = "6d1e7f3a-0000-4000-8000-0000000000d1";
    private const string BlenderId = "6d1e7f3a-0000-4000-8000-0000000000d2";
    private const string Where = "command 'Magnifier' in 'Global'";
    private const string RightHold = """{ "buttons": "Right" }""";
    private const string PlainBlender = """{ "id": "6d1e7f3a-0000-4000-8000-0000000000d2", "name": "Blender", "matcher": { "processNames": ["blender.exe"] } }""";

    private static readonly StepRegistry Registry = new([FakeStepType.Instance, RemapStepType.Instance]);
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));
    private static readonly RemapOutput WinShiftX = new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta);

    private readonly List<string> _notices = [];

    [Fact]
    public void TheMagnifierAndAnOwnButtonTrigger_RoundTrip_ByteStable()
    {
        var (mapping, blender) = Magnifier();
        var ownTrigger = Trigger.ForButton(MouseButton.X2, new TriggerHold(HeldButtons.X1, KeyModifiers.Meta, HoldCapture.Before, DragDistancePx: 4));
        var forward = NewCommand("Forward", Trigger.ForButton(MouseButton.Right, new TriggerHold(HeldButtons.Left)), NewStep("forward"))
            .WithTriggerFor(HostPlatform.MacOS, ownTrigger, DateTimeOffset.UnixEpoch);
        mapping = MappingRules.ValidDocument(mapping with { Groups = [mapping.Global with { Commands = [.. mapping.Global.Commands, forward] }] });

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, Registry, _notices.Add).Mapping;

        Assert.Empty(_notices);
        var magnifier = back.Global.Commands.Single(command => command.Name == "Magnifier");
        Assert.Equal(RightLeft, magnifier.Trigger);
        Assert.Equal(new RemapStep(WinShiftX), Assert.Single(magnifier.Steps).Step);
        Assert.Equal([blender.Id], magnifier.AlsoIn);
        var backForward = back.Global.Commands.Single(command => command.Name == "Forward");
        Assert.Equal(forward.Trigger, backForward.Trigger);
        Assert.Equal(ownTrigger, backForward.OwnVersion!.Trigger);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back) }));
    }

    [Fact]
    public void TheMembersAreWrittenWhereTheReadmeSays()
    {
        var (mapping, blender) = Magnifier();

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\"trigger\": {\n              \"button\": \"Left\",\n              \"hold\": {\n                \"buttons\": \"Right\"\n              }\n            },", json, StringComparison.Ordinal);
        Assert.Contains($"\"isActive\": true,\n            \"alsoIn\": [\n              \"{blender.Id}\"\n            ],\n            \"steps\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"params\": {\n                  \"output\": \"Key\",\n                  \"key\": \"X\",\n                  \"modifiers\": \"Shift, Meta\"\n                }", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandWithNotInAndAlsoIn_WritesNotInFirst()
    {
        var spine = new IgnoredApp(GroupId.New(), "Spine", IsActive: true, ByProcess("spine.exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        var blender = Excluded("Blender");
        var zoom = NewCommand("Zoom In", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("zoom")) with { NotIn = [spine.Id], AlsoIn = [blender.Id] };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(zoom)], [spine, blender]));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping }).Replace("\r\n", "\n", StringComparison.Ordinal);
        var back = ConfigSerializer.Read(json, Registry, _notices.Add).Mapping.Global.Commands.Single();

        Assert.Contains($"\"notIn\": [\n              \"{spine.Id}\"\n            ],\n            \"alsoIn\": [\n              \"{blender.Id}\"\n            ],", json, StringComparison.Ordinal);
        Assert.Equal([spine.Id], back.NotIn);
        Assert.Equal([blender.Id], back.AlsoIn);
        Assert.Empty(_notices);
    }

    [Fact]
    public void NeitherIsWrittenWhenUnset_AndASchemaSixFileReadsAsNone()
    {
        var plain = ConfigSerializer.Write(new ConfigDocument
        {
            Mapping = MappingRules.ValidDocument(new MappingDocument(
                [NewGlobal(NewCommand("Zoom In", Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right)), NewStep("zoom")), NewCommand("Close", Up, NewStep("close")))],
                [Excluded("Blender")])),
        });

        var back = ConfigSerializer.Read(
            $$"""
            { "schemaVersion": 6, "mapping": { "groups": [
              { "id": "{{GlobalId}}", "name": "Global", "commands": [
                { "id": "{{MagnifierId}}", "name": "Zoom In", "trigger": { "wheel": "Down", "hold": {{RightHold}} }, "steps": [] } ] } ],
              "ignored": [ {{PlainBlender}} ] } }
            """,
            Registry,
            _notices.Add);

        Assert.DoesNotContain("alsoIn", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("\"button\"", plain, StringComparison.Ordinal);
        Assert.Equal(ConfigDocument.CurrentSchemaVersion, back.SchemaVersion);
        Assert.Empty(back.Mapping.Global.Commands.Single().AlsoIn);
        Assert.Empty(_notices);
    }

    [Theory]
    [InlineData("""{ "button": "Thumb", "hold": { "buttons": "Right" } }""", "", $"'button' of 'trigger' of {Where} must be one of Left, Middle, Right, X1, X2.")]
    [InlineData("""{ "button": 1, "hold": { "buttons": "Right" } }""", "", $"'button' of 'trigger' of {Where} must be a string.")]
    [InlineData("""{ "button": "Left", "hold": { "buttons": "Right, Thumb" } }""", "", $"'buttons' of 'hold' of 'trigger' of {Where} must be a comma-separated list of None, Stroke, Left, Middle, Right, X1, X2.")]
    [InlineData("""{ "button": "Left", "hold": { "buttons": "Right" } }""", """, "alsoIn": "Blender" """, $"'alsoIn' of {Where} must be an array.")]
    [InlineData("""{ "button": "Left", "hold": { "buttons": "Right" } }""", """, "alsoIn": [ 3 ] """, $"'alsoIn' of {Where} must be an array of strings.")]
    public void AMemberOfTheWrongShape_IsAFormatErrorNamingIt(string trigger, string alsoIn, string message)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => Read(trigger, alsoIn));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void AnAlsoInEntryThatIsNotAGuid_IsDroppedWithANotice_AnUnknownEntryByTheRules()
    {
        const string unknown = "6d1e7f3a-0000-4000-8000-0000000000ff";

        var back = Read("""{ "button": "Left", "hold": { "buttons": "Right" } }""", $$""", "alsoIn": [ "Blender", "{{unknown}}", "{{BlenderId}}" ]""");

        Assert.Equal(["An entry of 'alsoIn' of command 'Magnifier' in 'Global' dropped: \"Blender\" is not a Guid string."], _notices);
        Assert.Equal([new GroupId(Guid.Parse(unknown)), new GroupId(Guid.Parse(BlenderId))], back.Global.Commands.Single().AlsoIn);
        Assert.Equal([new GroupId(Guid.Parse(BlenderId))], new MappingStore(back).Current.Global.Commands.Single().AlsoIn);
    }

    /// <summary>The writer never writes one: a button trigger without a <c>hold</c> holds the stroke button, which the rules refuse.</summary>
    [Fact]
    public void AButtonTriggerWithoutAHold_HoldsTheStrokeButton_WhichTheRulesRefuse()
    {
        var back = Read("""{ "button": "Left" }""");

        Assert.Equal(HeldButtons.Stroke, back.Global.Commands.Single().Trigger.Hold.Buttons);
        var ex = Assert.Throws<MappingValidationException>(() => new MappingStore(back));
        Assert.Equal("'Magnifier' holds the stroke button: a button trigger holds another button, as Right in Right + Left.", ex.Message);
    }

    /// <summary>
    /// The fallback loads every group before the ignored apps: each command's "Also in" (and "Not in") is put back once the
    /// entries are in, without an entry that did not load.
    /// </summary>
    [Fact]
    public void AMappingBreakingARule_LoadsRuleByRule_AndKeepsTheAlsoIn()
    {
        var (mapping, blender) = Magnifier();
        var spine = new IgnoredApp(GroupId.New(), "Spine", IsActive: true, ByProcess("spine.exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        var twin = Excluded("BLENDER");
        var zoom = NewCommand("Zoom In", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("zoom")) with { NotIn = [spine.Id], AlsoIn = [blender.Id] };
        var magnifier = mapping.Global.Commands.Single() with { AlsoIn = [blender.Id, twin.Id] };
        var raw = new MappingDocument([NewGlobal(magnifier, zoom)], [.. mapping.Ignored, spine, twin]);
        var notices = new List<string>();

        using var session = new ConfigSession(new InMemoryConfigStore(new ConfigDocument { Mapping = raw }), new ManualScheduler().Schedule, notices.Add);

        var loaded = session.Mapping.Current.Global;
        Assert.Equal([blender.Id], loaded.Commands.Single(command => command.Name == "Magnifier").AlsoIn);
        Assert.Equal(RightLeft, loaded.Commands.Single(command => command.Name == "Magnifier").Trigger);
        var loadedZoom = loaded.Commands.Single(command => command.Name == "Zoom In");
        Assert.Equal([spine.Id], loadedZoom.NotIn);
        Assert.Equal([blender.Id], loadedZoom.AlsoIn);
        Assert.StartsWith($"Excluded app 'BLENDER' ({twin.Id}) skipped:", Assert.Single(notices), StringComparison.Ordinal);
        Assert.False(session.Mapping.Undo());
    }

    private static IgnoredApp Excluded(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false);

    /// <summary>
    /// Global's Magnifier (plan 0005's "Done when"): Left while holding Right, one Remap step to Win+Shift+X, Also in the plain
    /// Exclusions › Global entry Blender. Validated.
    /// </summary>
    private static (MappingDocument Mapping, IgnoredApp Blender) Magnifier()
    {
        var blender = Excluded("Blender");
        var magnifier = NewCommand("Magnifier", RightLeft, new CommandStep(new RemapStep(WinShiftX), HostPlatform.Windows)) with { AlsoIn = [blender.Id] };
        return (MappingRules.ValidDocument(new MappingDocument([NewGlobal(magnifier)], [blender])), blender);
    }

    private MappingDocument Read(string trigger, string alsoIn = "")
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": {{ConfigDocument.CurrentSchemaVersion}}, "mapping": { "groups": [
              { "id": "{{GlobalId}}", "name": "Global", "commands": [
                { "id": "{{MagnifierId}}", "name": "Magnifier", "trigger": {{trigger}}{{alsoIn}},
                  "steps": [ { "type": "remap", "params": { "output": "Key", "key": "X", "modifiers": "Shift, Meta" } } ] } ] } ],
              "ignored": [ {{PlainBlender}} ] } }
            """,
            Registry,
            _notices.Add).Mapping;
}
