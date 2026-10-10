using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// A command's own drag distance (a trigger hold's <c>dragDistancePx</c>) and a Global command's "Not in" (<c>notIn</c>) on
/// disk (schema 5, plan 0004): written only when set, a schema 4 file reads as neither, a hand edit out of range never fails a
/// load, and the session's rule-by-rule fallback keeps a "Not in" naming groups that load after Global.
/// </summary>
public sealed class NotInAndDragDistanceSerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string SpineId = "6d1e7f3a-0000-4000-8000-0000000000c1";
    private const string ZoomId = "6d1e7f3a-0000-4000-8000-0000000000c2";
    private const string Where = "command 'Zoom In' in 'Global'";

    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: 3));

    private readonly List<string> _notices = [];

    [Fact]
    public void ADragDistanceAndANotIn_RoundTrip_ByteStable_AnOwnTriggersDistanceToo()
    {
        var (mapping, spine, eyeris) = Zoom();
        var ownTrigger = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Middle, DragDistancePx: 200));
        var zoom = mapping.Global.Commands.Single().WithTriggerFor(HostPlatform.MacOS, ownTrigger, DateTimeOffset.UnixEpoch);
        mapping = MappingRules.ValidDocument(mapping with { Groups = [mapping.Global with { Commands = [zoom] }, spine, eyeris] });

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add).Mapping;

        Assert.Empty(_notices);
        var read = back.Global.Commands.Single();
        Assert.Equal(RightWheelDown, read.Trigger);
        Assert.Equal(ownTrigger, read.OwnVersion!.Trigger);
        Assert.Equal(new[] { spine.Id, eyeris.Id }.OrderBy(id => id.Value), read.NotIn);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back) }));
    }

    [Fact]
    public void TheMembersAreWrittenWhereTheReadmeSays()
    {
        var (mapping, _, _) = Zoom();
        var notIn = mapping.Global.Commands.Single().NotIn;

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\"hold\": {\n                \"buttons\": \"Right\",\n                \"dragDistancePx\": 3\n              }", json, StringComparison.Ordinal);
        Assert.Contains(
            $"\"isActive\": true,\n            \"notIn\": [\n              \"{notIn[0]}\",\n              \"{notIn[1]}\"\n            ],\n            \"steps\": [",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NeitherIsWrittenWhenUnset_AndASchemaFourFileReadsAsNeither()
    {
        var plain = ConfigSerializer.Write(new ConfigDocument
        {
            Mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Zoom In", Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right)))), NewGroup("Spine"))),
        });

        var back = Read(schemaVersion: 4, hold: """{ "buttons": "Right" }""");

        Assert.DoesNotContain("dragDistancePx", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("notIn", plain, StringComparison.Ordinal);
        var zoom = back.Global.Commands.Single();
        Assert.Null(zoom.Trigger.Hold.DragDistancePx);
        Assert.Empty(zoom.NotIn);
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
    [InlineData("""{ "buttons": "Right", "dragDistancePx": "3" }""", "", $"'dragDistancePx' of 'hold' of 'trigger' of {Where} must be an integer.")]
    [InlineData("""{ "buttons": "Right", "dragDistancePx": 2.5 }""", "", $"'dragDistancePx' of 'hold' of 'trigger' of {Where} must be an integer.")]
    [InlineData("""{ "buttons": "Right" }""", """, "notIn": "Spine" """, $"'notIn' of {Where} must be an array.")]
    [InlineData("""{ "buttons": "Right" }""", """, "notIn": [ 3 ] """, $"'notIn' of {Where} must be an array of strings.")]
    public void AMemberOfTheWrongJsonType_IsAFormatErrorNamingIt(string hold, string notIn, string message)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => Read(hold: hold, notIn: notIn));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void ANotInEntryThatIsNotAGuid_IsDroppedWithANotice_AnUnknownGroupByTheRules()
    {
        const string unknown = "6d1e7f3a-0000-4000-8000-0000000000ff";

        var back = Read(hold: """{ "buttons": "Right" }""", notIn: $$""", "notIn": [ "Spine", "{{unknown}}", "{{SpineId}}" ]""");

        Assert.Equal(["An entry of 'notIn' of command 'Zoom In' in 'Global' dropped: \"Spine\" is not a Guid string."], _notices);
        Assert.Equal([new GroupId(Guid.Parse(unknown)), new GroupId(Guid.Parse(SpineId))], back.Global.Commands.Single().NotIn);
        Assert.Equal([new GroupId(Guid.Parse(SpineId))], new MappingStore(back).Current.Global.Commands.Single().NotIn);
    }

    /// <summary>The fallback loads Global first, before the app groups its commands name: the "Not in" is put back once they are in.</summary>
    [Fact]
    public void AMappingBreakingARule_LoadsRuleByRule_AndKeepsTheNotIn()
    {
        var (mapping, spine, eyeris) = Zoom();
        var twin = NewGroup("EYERIS");
        var raw = mapping with { Groups = [mapping.Global, spine, eyeris, twin] };
        var notices = new List<string>();

        using var session = new ConfigSession(new InMemoryConfigStore(new ConfigDocument { Mapping = raw }), new ManualScheduler().Schedule, notices.Add);

        Assert.Equal(mapping.Global.Commands.Single().NotIn, session.Mapping.Current.Global.Commands.Single().NotIn);
        Assert.Equal(RightWheelDown, session.Mapping.Current.Global.Commands.Single().Trigger);
        Assert.StartsWith($"App group 'EYERIS' ({twin.Id}) skipped:", Assert.Single(notices), StringComparison.Ordinal);
        Assert.False(session.Mapping.Undo());
    }

    /// <summary>Global's Zoom In on Right + wheel down at 3 px, not in Spine or Eyeris; validated.</summary>
    private static (MappingDocument Mapping, AppGroup Spine, AppGroup Eyeris) Zoom()
    {
        var spine = NewGroup("Spine", ByProcess("spine.exe"));
        var eyeris = NewGroup("Eyeris", ByProcess("eyeris.exe"));
        var zoom = NewCommand("Zoom In", RightWheelDown, NewStep("zoom")) with { NotIn = [eyeris.Id, spine.Id] };
        return (MappingRules.ValidDocument(Document(NewGlobal(zoom), spine, eyeris)), spine, eyeris);
    }

    private MappingDocument Read(string hold, string notIn = "", int schemaVersion = ConfigDocument.CurrentSchemaVersion)
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": {{schemaVersion}}, "mapping": { "groups": [
              { "id": "{{GlobalId}}", "name": "Global", "commands": [
                { "id": "{{ZoomId}}", "name": "Zoom In", "trigger": { "wheel": "Down", "hold": {{hold}} }{{notIn}}, "steps": [] } ] },
              { "id": "{{SpineId}}", "name": "Spine", "matcher": { "processNames": ["spine.exe"] }, "commands": [] } ] } }
            """,
            FakeStepType.Registry,
            _notices.Add).Mapping;
}
