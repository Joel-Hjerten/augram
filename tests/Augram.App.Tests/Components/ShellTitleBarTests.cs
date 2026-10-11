using Augram.App.Components.Shell;
using Augram.App.Themes;
using Augram.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The glass look's shell (plan 0006 decision 5): top-level tabs in the window's own title bar, sub-tabs as wrapping pills.</summary>
public sealed class ShellTitleBarTests
{
    [AvaloniaFact]
    public void TheWindowExtendsIntoTheTitleBarAndShowsItsTitleThere()
    {
        Assert.True(ThemeSelector.DrawsTitleBar);
        var window = Show();

        Assert.True(window.ExtendClientAreaToDecorationsHint);
        var titleBar = TitleBar(window);
        Assert.True(WindowDragArea.GetIsEnabled(titleBar));
        Assert.Contains(titleBar.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == window.Title);
    }

    [AvaloniaFact]
    public void OnlyTheSelectedTopLevelTabShowsTheFlaresThatJoinItToThePage()
    {
        var window = Show();
        var tabs = TopTabs(window);

        var selected = tabs.Single(tab => tab.IsSelected);
        Assert.All(Flares(selected), flare => Assert.True(flare.IsVisible));
        Assert.All(tabs.Where(tab => !tab.IsSelected).SelectMany(Flares), flare => Assert.False(flare.IsVisible));
    }

    [AvaloniaFact]
    public void ClickingATopLevelTabInTheTitleBarSelectsIt()
    {
        var window = Show();
        // Headless hit testing uses the last rendered frame: render one first.
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var options = TopTabs(window).Single(tab => (string?)tab.Header == "Options");
        Assert.False(options.IsSelected);

        var centre = options.TranslatePoint(new Point(options.Bounds.Width / 2, options.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, Avalonia.Input.MouseButton.Left);
        window.MouseUp(centre, Avalonia.Input.MouseButton.Left);
        window.UpdateLayout();

        Assert.True(options.IsSelected);
    }

    [AvaloniaFact]
    public void SubTabsArePillsThatWrapOntoMoreRows()
    {
        var window = Show();
        var shell = window.GetVisualDescendants().OfType<Shell>().Single();
        shell.SelectedKey = "diagnostics.log";
        window.UpdateLayout();

        var sub = window.GetVisualDescendants().OfType<TabControl>().Single(tabs => tabs.Classes.Contains("sub") && tabs.IsEffectivelyVisible);
        Assert.Equal("SubTabPill", ThemeKey(sub.GetLogicalChildren().OfType<TabItem>().First()));
        Assert.Contains(sub.GetVisualDescendants().OfType<WrapPanel>(), panel => panel.Children.OfType<TabItem>().Any());
    }

    private static MainWindow Show()
    {
        var window = TestAppBuilder.Services.GetRequiredService<MainWindow>();
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Grid TitleBar(Window window) =>
        window.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "PART_TitleBar");

    private static List<TabItem> TopTabs(Window window) =>
        [.. window.GetVisualDescendants().OfType<TabControl>().Single(tabs => tabs.Classes.Contains("shell")).GetLogicalChildren().OfType<TabItem>()];

    private static IEnumerable<Avalonia.Controls.Shapes.Path> Flares(TabItem tab) =>
        tab.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Where(path => path.Name is "PART_FlareLeft" or "PART_FlareRight");

    private static string? ThemeKey(TabItem tab) =>
        Avalonia.Application.Current!.TryFindResource("SubTabPill", out var pill) && ReferenceEquals(tab.Theme, pill) ? "SubTabPill" : null;
}
