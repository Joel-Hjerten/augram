using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.TypeText;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// A configuration for the export and import tests over the starter gestures: Global with a Window category (Minimize on Down)
/// and Greet (types text, no trigger); Chrome (Close tab on Up, Terminal running a command line on Left); Blender, Windows only,
/// with its Space hold remap (Orbit: Left → Middle) beside Undo on Left; an ignored Game. So two steps may hold private text
/// (one in Global, one in Chrome) and Blender has none. Sessions are in memory and never save.
/// </summary>
internal static class TransferTestData
{
    public static GestureId Up => StarterGestures.IdFor("Up");

    public static GestureId Down => StarterGestures.IdFor("Down");

    public static GestureId Left => StarterGestures.IdFor("Left");

    public static ConfigDocument Configuration()
    {
        var window = new CommandCategory(CategoryId.New(), "Window");
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [window],
            Commands =
            [
                Cmd("Minimize", Trigger.ForGesture(Down), new DelayStep(10)) with { CategoryId = window.Id },
                Cmd("Greet", Trigger.None, new TypeTextStep("Hello")),
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] },
        [
            Cmd("Close tab", Trigger.ForGesture(Up), new DelayStep(30)),
            Cmd("Terminal", Trigger.ForGesture(Left), new RunStep("wt.exe")),
        ]);
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, new AppMatcher { WindowsProcessNames = ["game.exe"] }, DisableEntirely: true);
        return new ConfigDocument
        {
            Settings = Settings.Default,
            Gestures = StarterGestures.All(),
            Mapping = MappingRules.ValidDocument(new MappingDocument([global, chrome, Blender()], [game])),
        };
    }

    /// <summary>Blender, Windows only: Undo on Left and the Space hold remap with Orbit (Left → Middle).</summary>
    public static AppGroup Blender()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var orbit = new Command(CommandId.New(), "Orbit", Trigger.ForInput(HoldInput.Of(MouseButton.Left)), IsActive: true, [new CommandStep(new RemapStep(new RemapOutput.Button(MouseButton.Middle)), HostPlatform.Windows)])
        {
            HoldRemapId = space.Id,
        };
        return new AppGroup(GroupId.New(), "Blender", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["blender.exe"] },
            [Cmd("Undo", Trigger.ForGesture(Left), new DelayStep(10)), orbit])
        {
            HoldRemaps = [space],
            UseOn = PlatformSet.Windows,
        };
    }

    /// <summary>A session over <paramref name="document"/> that never saves: the stores and their undo, nothing on disk.</summary>
    public static ConfigSession Session(ConfigDocument document) => new(new MemoryStore(document), _ => NoSave.Instance);

    public static AppGroup GroupNamed(MappingDocument mapping, string name) => mapping.Groups.Single(group => group.Name == name);

    public static Command CommandNamed(MappingDocument mapping, string group, string name) => GroupNamed(mapping, group).Commands.Single(command => command.Name == name);

    /// <summary>The document with <paramref name="change"/> applied to the group named <paramref name="name"/>, validated.</summary>
    public static ConfigDocument WithGroup(ConfigDocument document, string name, Func<AppGroup, AppGroup> change)
        => document with { Mapping = MappingRules.ValidDocument(document.Mapping with { Groups = [.. document.Mapping.Groups.Select(group => group.Name == name ? change(group) : group)] }) };

    /// <summary>The document without the group named <paramref name="name"/>: a configuration that lacks it.</summary>
    public static ConfigDocument WithoutGroup(ConfigDocument document, string name)
        => document with { Mapping = MappingRules.ValidDocument(document.Mapping with { Groups = [.. document.Mapping.Groups.Where(group => group.Name != name)] }) };

    /// <summary>The document with the starter gesture <paramref name="name"/> drawn as the starter <paramref name="drawnAs"/> (same id, other samples).</summary>
    public static ConfigDocument Redrawn(ConfigDocument document, string name, string drawnAs)
    {
        var samples = document.Gestures.Single(gesture => gesture.Name == drawnAs).Samples;
        return document with { Gestures = [.. document.Gestures.Select(gesture => gesture.Name == name ? gesture with { Samples = samples } : gesture)] };
    }

    public static Command Cmd(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, [.. steps.Select(step => new CommandStep(step, HostPlatform.Windows))]);

    private sealed class MemoryStore : IConfigStore
    {
        private readonly ConfigDocument _loaded;

        public MemoryStore(ConfigDocument loaded)
        {
            _loaded = loaded;
        }

        public string Location => "memory://augram.json";

        public ConfigDocument Load() => _loaded;

        public void Save(ConfigDocument document)
        {
        }
    }

    private sealed class NoSave : IDisposable
    {
        public static NoSave Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
