using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.TypeText;
using Augram.Core.Sync;
using Augram.Core.Tests.Config;
using Augram.Core.Tests.Fixtures;
using Augram.Core.Tests.HoldRemaps.Support;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Transfer;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Transfer.Support;

/// <summary>
/// A whole configuration for the export and import tests, with fresh ids on every call but the same names and shapes, so two
/// calls are the same setup on two machines that never shared ids: the gestures Up, Down and Left (stock flicks, three
/// shapes); Global with a Window category (Minimize on Up) and Close on Down; Chrome with Close tab on Down; Blender with its
/// Space hold remap (plan 0002); an ignored Game; options off their defaults, with a sync section.
/// </summary>
internal static class TransferSamples
{
    public const string RepositoryUrl = "https://github.com/joel/augram-settings";

    public static StepRegistry Registry { get; } = new([FakeStepType.Instance, RemapStepType.Instance, TypeTextStepType.Instance, RunStepType.Instance]);

    /// <summary>A gesture drawn as the stock flick <paramref name="shape"/> (its own name by default).</summary>
    public static Gesture Flick(string name, string? shape = null) => TestGestures.Create(name, StockFlicks.Template(shape ?? name));

    public static ConfigDocument SampleConfig()
    {
        var up = Flick("Up");
        var down = Flick("Down");
        var left = Flick("Left");
        var window = NewCategory("Window");
        var global = NewGlobal(NewCommand("Minimize", up.Id, NewStep("min")).In(window), NewCommand("Close", down.Id, NewStep("close"))) with { Categories = [window] };
        var chrome = NewGroup("Chrome", null, NewCommand("Close tab", down.Id, NewStep("ctrl+w")));
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true);
        return new ConfigDocument
        {
            Settings = SampleDocuments.NonDefaultSettings with { Sync = new SyncSettings(RepositoryUrl, Guid.NewGuid(), "PC-HOME") },
            Gestures = [up, down, left],
            Mapping = MappingRules.ValidDocument(new MappingDocument([global, chrome, Blender.Group()], [game])),
        };
    }

    /// <summary>A fresh install: default options, no gestures, just the Global group.</summary>
    public static ConfigDocument Empty() => new() { Gestures = [], Mapping = MappingDocument.Empty };

    /// <summary>The canonical text of every item by its key's text: two configurations compare item by item.</summary>
    public static SortedDictionary<string, string> Contents(IReadOnlyList<Gesture> gestures, MappingDocument? mapping)
        => new(
            SyncItemSet.From(gestures, mapping ?? MappingDocument.Empty).Contents().ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            StringComparer.Ordinal);

    public static SortedDictionary<string, string> Contents(ConfigDocument document) => Contents(document.Gestures, document.Mapping);

    public static SortedDictionary<string, string> Contents(ImportResult result) => Contents(result.Gestures, result.Mapping);

    /// <summary>Written and read back, as a file on disk would be.</summary>
    public static TransferFile RoundTrip(TransferFile file) => TransferSerializer.Read(TransferSerializer.Write(file), Registry);

    /// <summary>What exporting <paramref name="scope"/> from <paramref name="document"/> and reading the file gives.</summary>
    public static TransferFile Exported(ConfigDocument document, ExportScope scope) => RoundTrip(Exporter.Export(scope, document));

    /// <summary>A session over <paramref name="document"/> (in memory, saves by hand), so a plan is made from the stores' snapshots.</summary>
    public static ConfigSession Session(ConfigDocument document) => new(new InMemoryConfigStore(document), new ManualScheduler().Schedule);

    public static AppGroup GroupNamed(MappingDocument mapping, string name) => mapping.Groups.Single(group => group.Name == name);

    public static Command CommandNamed(MappingDocument mapping, string group, string command) => GroupNamed(mapping, group).Commands.Single(candidate => candidate.Name == command);

    public static Gesture GestureNamed(IReadOnlyList<Gesture> gestures, string name) => gestures.Single(gesture => gesture.Name == name);

    /// <summary>The document with <paramref name="change"/> applied to one group, validated.</summary>
    public static ConfigDocument WithGroup(ConfigDocument document, string name, Func<AppGroup, AppGroup> change)
        => document with
        {
            Mapping = MappingRules.ValidDocument(document.Mapping with { Groups = [.. document.Mapping.Groups.Select(group => group.Name == name ? change(group) : group)] }),
        };
}
