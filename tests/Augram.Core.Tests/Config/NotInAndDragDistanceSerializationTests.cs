using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// A command's own drag distance (a trigger hold's <c>dragDistancePx</c>), its "Not in" (<c>notIn</c>) and the Ignored › Per
/// command entries it names (an ignored app's <c>scope</c>) on disk (schemas 5 and 6, plan 0004): written only when set, older
/// files read as none of them, a schema 5 "Not in" naming app groups is dropped, a hand edit out of range never fails a load,
/// and the session's rule-by-rule fallback keeps a "Not in" naming entries that load after the groups.
/// </summary>
public sealed class NotInAndDragDistanceSerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string SpineId = "6d1e7f3a-0000-4000-8000-0000000000c1";
    private const string ZoomId = "6d1e7f3a-0000-4000-8000-0000000000c2";
    private const string SteamId = "6d1e7f3a-0000-4000-8000-0000000000c3";
    private const string Where = "command 'Zoom In' in 'Global'";
    private const string PerCommandSpine = """{ "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Spine", "scope": "PerCommand", "matcher": { "processNames": ["spine.exe"] } }""";

    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: 3));

    private readonly List<string> _notices = [];

    [Fact]
    public void ADragDistanceANotInAndAPerCommandEntry_RoundTrip_ByteStable_AnOwnTriggersDistanceToo()
    {
        var (mapping, spine, eyeris) = Zoom();
        var ownTrigger = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Middle, DragDistancePx: 200));
        var zoom = mapping.Global.Commands.Single().WithTriggerFor(HostPlatform.MacOS, ownTrigger, DateTimeOffset.UnixEpoch);
        mapping = MappingRules.ValidDocument(mapping with { Groups = [mapping.Global with { Commands = [zoom] }, .. mapping.Groups.Where(group => !group.IsGlobal)] });

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add).Mapping;

        Assert.Empty(_notices);
        var read = back.Global.Commands.Single();
        Assert.Equal(RightWheelDown, read.Trigger);
        Assert.Equal(ownTrigger, read.OwnVersion!.Trigger);
        Assert.Equal(new[] { spine.Id, eyeris.Id }.OrderBy(id => id.Value), read.NotIn);
        Assert.Equal([spine.Id], back.Groups.Single(group => group.Name == "Steam").Commands.Single().NotIn);
        Assert.Equal([IgnoreScope.PerCommand, IgnoreScope.PerCommand, IgnoreScope.Global], back.Ignored.Select(app => app.Scope));
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back) }));
    }

    [Fact]
    public void TheMembersAreWrittenWhereTheReadmeSays()
    {
        var (mapping, spine, _) = Zoom();
        var notIn = mapping.Global.Commands.Single().NotIn;

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\"hold\": {\n                \"buttons\": \"Right\",\n                \"dragDistancePx\": 3\n              }", json, StringComparison.Ordinal);
        Assert.Contains(
            $"\"isActive\": true,\n            \"notIn\": [\n              \"{notIn[0]}\",\n              \"{notIn[1]}\"\n            ],\n            \"steps\": [",
            json,
            StringComparison.Ordinal);
        Assert.Contains($"\"id\": \"{spine.Id}\",\n        \"name\": \"Spine\",\n        \"scope\": \"PerCommand\",\n        \"isActive\": true,", json, StringComparison.Ordinal);
        Assert.Equal(2, json.Split("\"scope\"").Length - 1);
    }

    [Fact]
    public void NoneIsWrittenWhenUnset_AndASchemaFourFileReadsAsNone()
    {
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, ByProcess("vmware.exe"), DisableEntirely: true);
        var plain = ConfigSerializer.Write(new ConfigDocument
        {
            Mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(NewCommand("Zoom In", Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right)))), NewGroup("Spine")], [vmware])),
        });

        var back = Read(schemaVersion: 4, hold: """{ "buttons": "Right" }""", ignored: """{ "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Spine" }""");

        Assert.DoesNotContain("dragDistancePx", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("notIn", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("scope", plain, StringComparison.Ordinal);
        var zoom = back.Global.Commands.Single();
        Assert.Null(zoom.Trigger.Hold.DragDistancePx);
        Assert.Empty(zoom.NotIn);
        Assert.Equal(IgnoreScope.Global, back.Ignored.Single().Scope);
        Assert.Empty(_notices);
    }

    /// <summary>Plan 0004, decision 6: a 0.8.0 "Not in" named app groups; it names no Per command entry, so the rules drop it.</summary>
    [Fact]
    public void ASchemaFiveNotInNamingAnAppGroup_IsDroppedByTheRules_TheRestLoads()
    {
        var back = Read(
            schemaVersion: 5,
            hold: """{ "buttons": "Right", "dragDistancePx": 3 }""",
            notIn: $$""", "notIn": [ "{{SteamId}}" ]""",
            ignored: """{ "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Spine" }""");

        var zoom = new MappingStore(back).Current.Global.Commands.Single();
        Assert.Empty(zoom.NotIn);
        Assert.Equal(RightWheelDown, zoom.Trigger);
        Assert.Empty(_notices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(201)]
    public void ADistanceOutOfRange_IsDroppedWithANotice_TheCommandUsesTheOptionsValue(int distance)
    {
        var back = Read(hold: $$"""{ "buttons": "Right", "dragDistancePx": {{distance}} }""");

        var zoom = back.Global.Commands.Single();
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right)), zoom.Trigger);
        Assert.Equal($"'dragDistancePx' of 'hold' of 'trigger' of {Where} dropped: {distance} is not between 1 and 200; the Options value is used.", Assert.Single(_notices));
    }

    [Fact]
    public void ADistanceOnASetHoldingTheStrokeButton_IsDroppedByTheRules()
    {
        var back = Read(hold: """{ "buttons": "Stroke, Left", "dragDistancePx": 5 }""");

        Assert.Null(new MappingStore(back).Current.Global.Commands.Single().Trigger.Hold.DragDistancePx);
        Assert.Empty(_notices);
    }

    [Theory]
    [InlineData("""{ "buttons": "Right", "dragDistancePx": "3" }""", "", PerCommandSpine, $"'dragDistancePx' of 'hold' of 'trigger' of {Where} must be an integer.")]
    [InlineData("""{ "buttons": "Right", "dragDistancePx": 2.5 }""", "", PerCommandSpine, $"'dragDistancePx' of 'hold' of 'trigger' of {Where} must be an integer.")]
    [InlineData("""{ "buttons": "Right" }""", """, "notIn": "Spine" """, PerCommandSpine, $"'notIn' of {Where} must be an array.")]
    [InlineData("""{ "buttons": "Right" }""", """, "notIn": [ 3 ] """, PerCommandSpine, $"'notIn' of {Where} must be an array of strings.")]
    [InlineData("""{ "buttons": "Right" }""", "", """{ "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Spine", "scope": "Commands" }""", "'scope' of ignored app 'Spine' must be one of Global, PerCommand.")]
    [InlineData("""{ "buttons": "Right" }""", "", """{ "id": "6d1e7f3a-0000-4000-8000-0000000000c1", "name": "Spine", "scope": 1 }""", "'scope' of ignored app 'Spine' must be a string.")]
    public void AMemberOfTheWrongJsonType_IsAFormatErrorNamingIt(string hold, string notIn, string ignored, string message)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => Read(hold: hold, notIn: notIn, ignored: ignored));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void ANotInEntryThatIsNotAGuid_IsDroppedWithANotice_AnUnknownEntryByTheRules()
    {
        const string unknown = "6d1e7f3a-0000-4000-8000-0000000000ff";

        var back = Read(hold: """{ "buttons": "Right" }""", notIn: $$""", "notIn": [ "Spine", "{{unknown}}", "{{SpineId}}" ]""");

        Assert.Equal(["An entry of 'notIn' of command 'Zoom In' in 'Global' dropped: \"Spine\" is not a Guid string."], _notices);
        Assert.Equal([new GroupId(Guid.Parse(unknown)), new GroupId(Guid.Parse(SpineId))], back.Global.Commands.Single().NotIn);
        Assert.Equal([new GroupId(Guid.Parse(SpineId))], new MappingStore(back).Current.Global.Commands.Single().NotIn);
    }

    /// <summary>
    /// The fallback loads every group before the ignored apps: each command's "Not in" (Global's and an app group's) is put back
    /// once the Per command entries are in, without an entry that did not load.
    /// </summary>
    [Fact]
    public void AMappingBreakingARule_LoadsRuleByRule_AndKeepsTheNotIn()
    {
        var (mapping, spine, eyeris) = Zoom();
        var twinGroup = NewGroup("STEAM");
        var twinApp = PerCommand("EYERIS");
        var global = mapping.Global with { Commands = [mapping.Global.Commands.Single() with { NotIn = [spine.Id, eyeris.Id, twinApp.Id] }] };
        var raw = new MappingDocument([global, .. mapping.Groups.Where(group => !group.IsGlobal), twinGroup], [.. mapping.Ignored, twinApp]);
        var notices = new List<string>();

        using var session = new ConfigSession(new InMemoryConfigStore(new ConfigDocument { Mapping = raw }), new ManualScheduler().Schedule, notices.Add);

        var loaded = session.Mapping.Current;
        Assert.Equal(mapping.Global.Commands.Single().NotIn, loaded.Global.Commands.Single().NotIn);
        Assert.Equal([spine.Id], loaded.Groups.Single(group => group.Name == "Steam").Commands.Single().NotIn);
        Assert.Equal(RightWheelDown, loaded.Global.Commands.Single().Trigger);
        Assert.Equal(IgnoreScope.PerCommand, loaded.Ignored.Single(app => app.Id == spine.Id).Scope);
        Assert.Collection(
            notices,
            notice => Assert.StartsWith($"App group 'STEAM' ({twinGroup.Id}) skipped:", notice, StringComparison.Ordinal),
            notice => Assert.StartsWith($"Excluded app 'EYERIS' ({twinApp.Id}) skipped:", notice, StringComparison.Ordinal));
        Assert.False(session.Mapping.Undo());
    }

    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    /// <summary>
    /// Global's Zoom In on Right + wheel down at 3 px, not in the Per command entries Spine and Eyeris; Steam's Steam zoom not in
    /// Spine; VMware on Ignored › Global. Validated.
    /// </summary>
    private static (MappingDocument Mapping, IgnoredApp Spine, IgnoredApp Eyeris) Zoom()
    {
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, ByProcess("vmware.exe"), DisableEntirely: true);
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [eyeris.Id, spine.Id] };
        var steam = NewGroup("Steam", ByProcess("steam.exe"), NewCommand("Steam zoom", Up, NewStep("steam")) with { NotIn = [spine.Id] });
        return (MappingRules.ValidDocument(new MappingDocument([NewGlobal(zoom), steam], [spine, eyeris, vmware])), spine, eyeris);
    }

    private MappingDocument Read(string hold, string notIn = "", string ignored = PerCommandSpine, int schemaVersion = ConfigDocument.CurrentSchemaVersion)
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": {{schemaVersion}}, "mapping": { "groups": [
              { "id": "{{GlobalId}}", "name": "Global", "commands": [
                { "id": "{{ZoomId}}", "name": "Zoom In", "trigger": { "wheel": "Down", "hold": {{hold}} }{{notIn}}, "steps": [] } ] },
              { "id": "{{SteamId}}", "name": "Steam", "matcher": { "processNames": ["steam.exe"] }, "commands": [] } ],
              "ignored": [ {{ignored}} ] } }
            """,
            FakeStepType.Registry,
            _notices.Add).Mapping;
}
