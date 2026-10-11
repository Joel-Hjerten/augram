using Augram.App.Hosting;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Navigation;

/// <summary>
/// The app's tabs (F7): Commands (Global / Apps) · Exclusions (Global / Per command; "ignored" in the keys and the code) ·
/// Gestures · Options (General / Strokes / Appearance / Sync / About, plan 0006 decision 9) · Diagnostics (Health / Log /
/// Recognition), plus the dev Gallery in Debug builds (Joel, 2026-10-10: Commands and Exclusions before Gestures). Adding
/// a tab is adding an entry here and a screen under <c>Screens/</c>.
/// </summary>
public static class AppNavigation
{
    public const string GesturesKey = "gestures";
    public const string CommandsKey = "commands";
    public const string CommandsGlobalKey = "commands.global";
    public const string CommandsAppsKey = "commands.apps";
    public const string IgnoredKey = "ignored";
    public const string IgnoredGlobalKey = "ignored.global";
    public const string IgnoredPerCommandKey = "ignored.percommand";
    public const string OptionsKey = "options";
    public const string OptionsGeneralKey = "options.general";
    public const string OptionsStrokesKey = "options.strokes";
    public const string OptionsAppearanceKey = "options.appearance";
    public const string OptionsSyncKey = "options.sync";
    public const string OptionsAboutKey = "options.about";
    public const string DiagnosticsKey = "diagnostics";

    public static NavigationRegistry Build(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AppSettingsViewModel Settings() => services.GetRequiredService<AppSettingsViewModel>();
        var entries = new List<NavEntry>
        {
            CommandsModule.NavEntry(services),
            IgnoredModule.NavEntry(services),
            GesturesModule.NavEntry(services),
            new("Options", OptionsKey, SubEntries:
            [
                new(OptionsScreen.GeneralTitle, OptionsGeneralKey, () => OptionsScreen.DeclareGeneral(Settings())),
                new(OptionsScreen.StrokesTitle, OptionsStrokesKey, () => OptionsScreen.DeclareStrokes(Settings())),
                new(OptionsScreen.AppearanceTitle, OptionsAppearanceKey, () => OptionsScreen.DeclareAppearance(Settings())),
                new(OptionsScreen.SyncTitle, OptionsSyncKey, () => OptionsScreen.DeclareSync(services.GetService<SyncViewModel>(), services.GetService<ConfigurationViewModel>())),
                new(OptionsScreen.AboutTitle, OptionsAboutKey, () => OptionsScreen.DeclareAbout(Settings())),
            ]),
            new("Diagnostics", DiagnosticsKey, SubEntries:
            [
                new("Health", "diagnostics.health", () => HealthScreen.Declare(services.GetRequiredService<HealthViewModel>())),
                new("Log", "diagnostics.log", () => LogScreen.Declare(services.GetRequiredService<LogViewModel>())),
                new("Recognition", "diagnostics.recognition", () => RecognitionScreen.Declare(services.GetRequiredService<RecognitionViewModel>())),
            ]),
        };
#if DEBUG
        entries.Add(DevGallery.GalleryNavigation.Entry());
#endif
        return new NavigationRegistry(entries);
    }
}
