using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Tests.Fixtures;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>
/// The one sample world of the Sync and Transfer tests (<see cref="World"/>), and the small but complete setup the sync tests
/// use (<see cref="Setup"/>: gestures, a categorised Global, an app group, an ignored app), with fresh ids on every call.
/// </summary>
internal static class SyncSamples
{
    public static Gesture NewGesture(string name) => TestGestures.Create(name, [new GesturePoint(0, 0), new GesturePoint(100, 0)]);

    public static (Gesture[] Gestures, MappingDocument Mapping) Setup() => World(NewGesture);

    /// <summary>
    /// Fresh ids on every call, the same names: the gestures Up, Down and Left, each drawn by <paramref name="gesture"/>; Global
    /// with a Window category (Minimize on Up) and Close on Down; Chrome with Close tab on Down; then <paramref name="moreGroups"/>;
    /// an ignored Game. The export and import tests add Blender and draw stock flicks (<c>Transfer/Support/TransferSamples</c>).
    /// </summary>
    public static (Gesture[] Gestures, MappingDocument Mapping) World(Func<string, Gesture> gesture, params AppGroup[] moreGroups)
    {
        var up = gesture("Up");
        var down = gesture("Down");
        var left = gesture("Left");
        var window = NewCategory("Window");
        var minimize = NewCommand("Minimize", up.Id, NewStep("min")).In(window);
        var global = NewGlobal(minimize, NewCommand("Close", down.Id, NewStep("close"))) with { Categories = [window] };
        var chrome = NewGroup("Chrome", null, NewCommand("Close tab", down.Id, NewStep("ctrl+w")));
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true);
        return ([up, down, left], MappingRules.ValidDocument(new MappingDocument([global, chrome, .. moreGroups], [game])));
    }
}
