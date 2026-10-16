using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>
/// An Exclusions › While holding entry on disk (schema 9, plan 0012): its <c>scope</c>, its <c>holding</c> and its
/// <c>ownHolding</c> round-trip byte-stable. The bump exists because a schema 8 build would refuse the scope's name, or, had it
/// read it, saved the file back without the inputs.
/// </summary>
public sealed class WhileHoldingSerializationTests
{
    private static readonly StepRegistry Registry = new([HotkeyStepType.Instance]);

    /// <summary>The PC's Alt, S and Middle; the Mac's own Cmd and Space.</summary>
    public static IgnoredApp AfterEffects()
    {
        var entry = WhileHolding("After Effects", new KeyOrButton.Modifier(KeyModifiers.Alt), new KeyOrButton.Key(KeyCode.S), new KeyOrButton.Button(MouseButton.Middle));
        return entry with
        {
            Holding = entry.Holding.WithFor(HostPlatform.MacOS, [new(new KeyOrButton.Modifier(KeyModifiers.Meta), HostPlatform.MacOS), new(new KeyOrButton.Key(KeyCode.Space), HostPlatform.MacOS)]),
        };
    }

    [Fact]
    public void AWhileHoldingEntry_RoundTripsByteStable_AtSchemaNine()
    {
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal()], [AfterEffects()]));

        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });
        var notices = new List<string>();
        var back = ConfigSerializer.Read(json, Registry, notices.Add);

        Assert.Empty(notices);
        Assert.True(ConfigDocument.CurrentSchemaVersion >= 9, "a schema 8 build must refuse the file");
        Assert.Contains($"\"schemaVersion\": {ConfigDocument.CurrentSchemaVersion}", json, StringComparison.Ordinal);
        Assert.Contains("\"scope\": \"WhileHolding\"", json, StringComparison.Ordinal);
        Assert.Contains("\"modifier\": \"Alt\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ownHolding\"", json, StringComparison.Ordinal);
        var entry = Assert.Single(back.Mapping.Ignored);
        Assert.Equal(IgnoreScope.WhileHolding, entry.Scope);
        Assert.Equal(mapping.Ignored[0].Holding.Original, entry.Holding.Original);
        Assert.Equal(mapping.Ignored[0].Holding.Own!.Items, entry.Holding.Own!.Items);
        Assert.Equal(HostPlatform.MacOS, entry.Holding.Own.Platform);
        Assert.Equal(json, ConfigSerializer.Write(new ConfigDocument { Mapping = MappingRules.ValidDocument(back.Mapping) }));
    }
}
