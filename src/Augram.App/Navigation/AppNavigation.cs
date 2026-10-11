using Augram.App.Hosting.Modules;

namespace Augram.App.Navigation;

/// <summary>
/// The app's tabs (F7): Commands (Global / Apps) · Exclusions (Global / Per command; "ignored" in the keys and the code) ·
/// Gestures · Options (General / Strokes / Appearance / Sync / About, plan 0006 decision 9) · Diagnostics (Health / Log /
/// Recognition), plus the dev Gallery in Debug builds (Joel, 2026-10-10: Commands and Exclusions before Gestures). Every tab
/// is its module's <c>NavEntry</c> (<c>Hosting/Modules/</c>); adding a tab is a module with its <c>NavEntry</c>, a line here and
/// a screen under <c>Screens/</c>.
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
        var entries = new List<NavEntry>
        {
            CommandsModule.NavEntry(services),
            IgnoredModule.NavEntry(services),
            GesturesModule.NavEntry(services),
            OptionsModule.NavEntry(services),
            DiagnosticsModule.NavEntry(services),
        };
#if DEBUG
        entries.Add(DevGallery.GalleryNavigation.Entry());
#endif
        return new NavigationRegistry(entries);
    }
}
