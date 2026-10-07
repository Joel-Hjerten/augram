using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Tests.Fixtures;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>A small but complete setup (gestures, a categorised Global, an app group, an ignored app) with fresh ids on every call.</summary>
internal static class SyncSamples
{
    public static Gesture NewGesture(string name) => TestGestures.Create(name, [new GesturePoint(0, 0), new GesturePoint(100, 0)]);

    public static (Gesture[] Gestures, MappingDocument Mapping) Setup()
    {
        var up = NewGesture("Up");
        var down = NewGesture("Down");
        var left = NewGesture("Left");
        var window = NewCategory("Window");
        var minimize = NewCommand("Minimize", up.Id, NewStep("min")).In(window);
        var global = NewGlobal(minimize, NewCommand("Close", down.Id, NewStep("close"))) with { Categories = [window] };
        var chrome = NewGroup("Chrome", null, NewCommand("Close tab", down.Id, NewStep("ctrl+w")));
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true);
        return ([up, down, left], MappingRules.ValidDocument(new MappingDocument([global, chrome], [game])));
    }
}
