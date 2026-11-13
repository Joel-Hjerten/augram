using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// Drag triggers and a Remap button output's held key on disk (schema 10, plan 0015): <c>{ "drag": true, "hold": { "buttons":
/// "Right" } }</c> in a command and an own version alike, and <c>"heldKey"</c> in the Remap step's params, written only when set.
/// </summary>
public sealed class DragTriggerSerializationTests
{
    private static readonly StepRegistry Registry = new([FakeStepType.Instance, RemapStepType.Instance]);
    private static readonly Trigger RightDrag = Trigger.ForDrag(new TriggerHold(HeldButtons.Right));
    private static readonly RemapOutput LeftWithSpace = new RemapOutput.Button(MouseButton.Left) { HeldKey = KeyCode.Space };

    private readonly List<string> _notices = [];

    private static MappingDocument Pan()
    {
        var own = Trigger.ForDrag(new TriggerHold(HeldButtons.X1, KeyModifiers.Meta, DragDistancePx: 4));
        var pan = NewCommand("Pan", RightDrag, new CommandStep(new RemapStep(LeftWithSpace), HostPlatform.Windows))
            .WithTriggerFor(HostPlatform.MacOS, own, DateTimeOffset.UnixEpoch);
        return MappingRules.ValidDocument(Document(NewGlobal(pan)));
    }

    [Fact]
    public void ADragTriggerWithAHeldKey_AndAnOwnDragTrigger_RoundTrip_ByteStable()
    {
        var mapping = Pan();

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var back = ConfigSerializer.Read(json, Registry, _notices.Add).Mapping;

        Assert.Empty(_notices);
        var pan = Assert.Single(back.Global.Commands);
        Assert.Equal(RightDrag, pan.Trigger);
        Assert.Equal(mapping.Global.Commands.Single().OwnVersion!.Trigger, pan.OwnVersion!.Trigger);
        Assert.Equal(new RemapStep(LeftWithSpace), Assert.Single(pan.Steps).Step);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back) }));
    }

    [Fact]
    public void TheMembersAreWrittenWhereTheReadmeSays_TheHeldKeyOnlyWhenSet()
    {
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = Pan() }).Replace("\r\n", "\n", StringComparison.Ordinal);
        var plain = RemapStepType.Instance.Write(new RemapStep(new RemapOutput.Button(MouseButton.Left))).ToJsonString();

        Assert.Contains("\"trigger\": {\n              \"drag\": true,\n              \"hold\": {\n                \"buttons\": \"Right\"\n              }\n            },", json, StringComparison.Ordinal);
        Assert.Contains("\"params\": {\n                  \"output\": \"Button\",\n                  \"button\": \"Left\",\n                  \"heldKey\": \"Space\"\n                }", json, StringComparison.Ordinal);
        Assert.Equal("{\"output\":\"Button\",\"button\":\"Left\"}", plain);
    }
}
