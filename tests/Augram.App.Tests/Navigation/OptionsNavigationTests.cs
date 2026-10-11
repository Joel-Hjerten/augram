using Augram.App.Components.Shell;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Navigation;

/// <summary>
/// Options as five sub-tabs (plan 0006 decision 9), as Diagnostics has them: General, Strokes (Capture, Recognition),
/// Appearance (Theme, Trail), Sync (Sync, Export and import) and About, every field where it was, and a key that selects each.
/// </summary>
public sealed class OptionsNavigationTests
{
    [AvaloniaFact]
    public void TheOptionsTabHasFiveSubTabs_WithTheirKeysInOrder()
    {
        var options = Options();

        Assert.Null(options.Screen);
        Assert.Equal(
            [
                ("General", AppNavigation.OptionsGeneralKey),
                ("Strokes", AppNavigation.OptionsStrokesKey),
                ("Appearance", AppNavigation.OptionsAppearanceKey),
                ("Sync", AppNavigation.OptionsSyncKey),
                ("About", AppNavigation.OptionsAboutKey),
            ],
            options.SubEntries!.Select(entry => (entry.Title, entry.Key)));
        Assert.Equal(["options.general", "options.strokes", "options.appearance", "options.sync", "options.about"], options.SubEntries!.Select(entry => entry.Key));
    }

    [AvaloniaFact]
    public void EachSubTabHasItsSections_WithEveryFieldWhereItWas()
    {
        var screens = Options().SubEntries!.ToDictionary(entry => entry.Key, entry => Assert.IsType<FormScreen>(entry.Screen!()));

        Assert.Equal(["General"], Titles(screens[AppNavigation.OptionsGeneralKey]));
        Assert.Equal(["Capture", "Recognition"], Titles(screens[AppNavigation.OptionsStrokesKey]));
        Assert.Equal([OptionsScreen.ThemeTitle, "Trail"], Titles(screens[AppNavigation.OptionsAppearanceKey]));
        Assert.Equal([OptionsSyncSection.Title, "Export and import"], Titles(screens[AppNavigation.OptionsSyncKey]));
        Assert.Equal([OptionsScreen.AboutTitle], Titles(screens[AppNavigation.OptionsAboutKey]));

        var general = Labels(screens[AppNavigation.OptionsGeneralKey], "General");
        Assert.Equal(
            AppState.CanChooseMenuBarIcon
                ? ["Stroke button", "Detect button", "Ignore keys", "Start at login", OptionsScreen.StartAtLoginNoteLabel, "Colour menu-bar icon", "Config folder"]
                : ["Stroke button", "Detect button", "Ignore keys", "Start at login", OptionsScreen.StartAtLoginNoteLabel, "Config folder"],
            general);
        Assert.Equal(["Start distance (px)", "Button drag distance (px)", "Cancel delay (ms)", "When nothing matches"], Labels(screens[AppNavigation.OptionsStrokesKey], "Capture"));
        Assert.Equal(["Threshold", "Precision", "Scoring mode"], Labels(screens[AppNavigation.OptionsStrokesKey], "Recognition"));
        Assert.Equal(["Theme", "Window background", "Tint", "Corner rounding", "Accent from trail colour", "Accent colour"], Labels(screens[AppNavigation.OptionsAppearanceKey], OptionsScreen.ThemeTitle));
        Assert.Equal(["Colour", "Width (px)", "Opacity"], Labels(screens[AppNavigation.OptionsAppearanceKey], "Trail"));
        Assert.Equal(["Version", "Commit", "Channel"], Labels(screens[AppNavigation.OptionsAboutKey], OptionsScreen.AboutTitle));

        // The trail colour is picked from the rainbow swatches or with Custom… (decision 8).
        var colour = Assert.IsType<ColorField>(screens[AppNavigation.OptionsAppearanceKey].Sections.Single(section => section.Title == "Trail").Fields[0]);
        Assert.Same(ColourPresets.Rainbow, colour.Presets);

        // The inspector names a field where the user finds it.
        Assert.Equal("Options › Strokes", screens[AppNavigation.OptionsStrokesKey].Title);
    }

    [AvaloniaFact]
    public void ASyncSubTabWithoutItsViewModels_ShowsWhatItHas()
    {
        Assert.Empty(Assert.IsType<FormScreen>(OptionsScreen.DeclareSync(null, null)).Sections);
        var sync = TestAppBuilder.Services.GetRequiredService<SyncViewModel>();
        Assert.Equal([OptionsSyncSection.Title], Titles(Assert.IsType<FormScreen>(OptionsScreen.DeclareSync(sync, null))));
    }

    [AvaloniaFact]
    public void EveryOptionsKeyLandsOnItsSubTab_ThroughTheTabNavigator()
    {
        var (shell, navigator) = ShowShell();
        Assert.Equal((AppNavigation.GesturesKey, null), Selected(shell));

        foreach (var key in new[] { AppNavigation.OptionsSyncKey, AppNavigation.OptionsGeneralKey, AppNavigation.OptionsStrokesKey, AppNavigation.OptionsAboutKey, AppNavigation.OptionsAppearanceKey })
        {
            Assert.True(navigator.Show(key));
            Assert.Equal((AppNavigation.OptionsKey, key), Selected(shell));
        }

        // The tab's own key still opens Options, on the sub-tab it shows.
        Assert.True(navigator.Show(AppNavigation.GesturesKey));
        Assert.True(navigator.Show(AppNavigation.OptionsKey));
        Assert.Equal((AppNavigation.OptionsKey, AppNavigation.OptionsAppearanceKey), Selected(shell));
    }

    private static NavEntry Options() =>
        TestAppBuilder.Services.GetRequiredService<NavigationRegistry>().Find(AppNavigation.OptionsKey)!;

    private static List<string> Titles(FormScreen screen) => [.. screen.Sections.Select(section => section.Title)];

    private static List<string> Labels(FormScreen screen, string section) =>
        [.. screen.Sections.Single(candidate => candidate.Title == section).Fields.Select(field => field.Label)];

    /// <summary>A shell with a Gestures stand-in and the real Options entry, its selected key bound to the main window view model as <c>MainWindow</c> binds it.</summary>
    private static (Shell Shell, TabNavigator Navigator) ShowShell()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MainWindowViewModel(new NavigationRegistry(
        [
            new NavEntry("Gestures", AppNavigation.GesturesKey, () => new TextScreen("Gestures", "stand-in")),
            Options(),
        ]))
        { InitialTabKey = AppNavigation.GesturesKey });
        var provider = services.BuildServiceProvider();
        var main = provider.GetRequiredService<MainWindowViewModel>();
        var shell = new Shell { Registry = main.Registry };
        shell.Bind(Shell.SelectedKeyProperty, new Binding(nameof(MainWindowViewModel.InitialTabKey)) { Source = main });
        new Window { Content = shell, Width = 900, Height = 700 }.Show();
        return (shell, new TabNavigator(provider));
    }

    /// <summary>The selected top tab's key and, when it has sub-tabs, the selected sub-tab's key.</summary>
    private static (string? Tab, string? SubTab) Selected(Shell shell)
    {
        var tab = (TabItem)shell.GetVisualDescendants().OfType<TabControl>().First().SelectedItem!;
        var sub = (tab.Content as TabControl)?.SelectedItem as TabItem;
        return ((string?)tab.Tag, (string?)sub?.Tag);
    }
}
