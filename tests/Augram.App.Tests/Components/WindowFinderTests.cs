using Augram.App.Components.WindowFinder;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>
/// The window finder in a headless window, driven by simulated pointer and key events over a <see cref="FakeWindowSystem"/>:
/// a left drag onto another app's window picks it at the release point with live feedback on the way, Augram's own windows
/// and empty screen pick nothing, Esc and a right-click cancel. No real pointer moves and no real window is read.
/// </summary>
public sealed class WindowFinderTests
{
    private static readonly WindowIdentity Chrome = FakeWindowSystem.Window("chrome.exe", "Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", ["Chrome_RenderWidgetHostHWND", "Chrome_WidgetWin_1"]);

    // Far outside the 300 x 200 test window: the drag leaves it, as a real one does.
    private static readonly Point OverChrome = new(1200, 100);
    private static readonly Point OverOwnWindow = new(1200, 400);
    private static readonly Point OverNothing = new(1200, 700);

    [AvaloniaFact]
    public void ALeftDragOntoAnotherAppsWindow_PicksItWhereReleased_WithLiveFeedbackOnTheWay()
    {
        var (window, finder, windows, picks) = Show();
        var idleCursor = finder.Cursor;

        Press(window, finder);
        Assert.True(finder.IsFinding);
        Assert.Contains(":finding", finder.Classes);
        Assert.Equal(WindowFinder.PromptText, finder.HoverText);
        Assert.NotNull(finder.Cursor);
        Assert.NotSame(idleCursor, finder.Cursor);
        Assert.True(Feedback(finder).IsOpen);

        window.MouseMove(OverChrome);
        Assert.Equal("chrome.exe · Google Chrome", finder.HoverText);
        window.MouseUp(OverChrome, MouseButton.Left);

        Assert.Same(Chrome, Assert.Single(picks));
        var screen = window.PointToScreen(OverChrome);
        Assert.Equal((screen.X, screen.Y), windows.Asked[^1]);
        Assert.False(finder.IsFinding);
        Assert.DoesNotContain(":finding", finder.Classes);
        Assert.Same(idleCursor, finder.Cursor);
        Assert.False(Feedback(finder).IsOpen);
    }

    [AvaloniaFact]
    public void OverAugramsOwnWindowOrOverNothing_TheFeedbackSaysSo_AndTheReleasePicksNothing()
    {
        var (window, finder, _, picks) = Show();

        Press(window, finder);
        window.MouseMove(OverOwnWindow);
        Assert.Equal(WindowFinder.OwnWindowText, finder.HoverText);
        window.MouseUp(OverOwnWindow, MouseButton.Left);

        Press(window, finder);
        window.MouseMove(OverNothing);
        Assert.Equal(WindowFinder.NoWindowText, finder.HoverText);
        window.MouseUp(OverNothing, MouseButton.Left);

        Assert.Empty(picks);
        Assert.False(finder.IsFinding);
    }

    [AvaloniaFact]
    public void EscOrARightClick_CancelsTheFind_AndTheLaterReleasePicksNothing()
    {
        var (window, finder, _, picks) = Show();

        Press(window, finder);
        window.MouseMove(OverChrome);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(finder.IsFinding);
        window.MouseMove(OverChrome);
        window.MouseUp(OverChrome, MouseButton.Left);

        Press(window, finder);
        window.MouseMove(OverChrome);
        window.MouseDown(OverChrome, MouseButton.Right);
        Assert.False(finder.IsFinding);
        window.MouseUp(OverChrome, MouseButton.Right);
        window.MouseUp(OverChrome, MouseButton.Left);

        Assert.Empty(picks);
    }

    [AvaloniaFact]
    public void AFieldsFinderDescribesWhatItWouldTake()
    {
        var (window, finder, _, _) = Show();
        finder.Describe = identity => identity.ProcessPath ?? "none";

        Press(window, finder);
        window.MouseMove(OverChrome);

        Assert.Equal(Chrome.ProcessPath, finder.HoverText);
        finder.Cancel();
        Assert.False(finder.IsFinding);
    }

    [AvaloniaFact]
    public void WithoutAnExplicitWindowSystem_TheResourceIsUsed_AndLeavingTheWindowEndsTheFind()
    {
        var (window, finder, windows, picks) = Show(explicitSystem: false);
        window.Resources[WindowFinder.WindowSystemResourceKey] = windows;

        Press(window, finder);
        window.MouseMove(OverChrome);
        window.MouseUp(OverChrome, MouseButton.Left);
        Assert.Same(Chrome, Assert.Single(picks));

        Press(window, finder);
        Assert.True(finder.IsFinding);
        ((Panel)window.Content!).Children.Clear();
        Assert.False(finder.IsFinding);
    }

    private static (Window Window, WindowFinder Finder, FakeWindowSystem Windows, List<WindowIdentity> Picks) Show(bool explicitSystem = true)
    {
        var finder = new WindowFinder { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Width = 300, Height = 200, Content = new Panel { Children = { finder } } };
        window.Show();
        var windows = new FakeWindowSystem()
            .Around(window.PointToScreen(OverChrome).X, window.PointToScreen(OverChrome).Y, Chrome)
            .Around(window.PointToScreen(OverOwnWindow).X, window.PointToScreen(OverOwnWindow).Y, FakeWindowSystem.OwnWindow());
        if (explicitSystem)
        {
            finder.WindowSystem = windows;
        }

        var picks = new List<WindowIdentity>();
        finder.Picked += (_, e) => picks.Add(e.Window);
        return (window, finder, windows, picks);
    }

    private static void Press(Window window, WindowFinder finder)
        => window.MouseDown(finder.TranslatePoint(new Point(finder.Bounds.Width / 2, finder.Bounds.Height / 2), window)!.Value, MouseButton.Left);

    private static Popup Feedback(WindowFinder finder) => finder.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "PART_Feedback");
}
