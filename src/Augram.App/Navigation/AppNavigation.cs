using Augram.App.Hosting;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Navigation;

/// <summary>
/// The app's tabs (F7): Gestures · Commands (Global / Apps) · Ignored · Options · Diagnostics
/// (Health / Log / Recognition), plus the dev Gallery in Debug builds. Adding a tab is adding an entry
/// here and a screen under <c>Screens/</c>.
/// </summary>
public static class AppNavigation
{
    public const string GesturesKey = "gestures";
    public const string CommandsKey = "commands";
    public const string CommandsGlobalKey = "commands.global";
    public const string CommandsAppsKey = "commands.apps";
    public const string IgnoredKey = "ignored";
    public const string OptionsKey = "options";
    public const string DiagnosticsKey = "diagnostics";

    public static NavigationRegistry Build(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var entries = new List<NavEntry>
        {
            GesturesModule.NavEntry(services),
            CommandsModule.NavEntry(services),
            IgnoredModule.NavEntry(services),
            new("Options", OptionsKey, () => OptionsScreen.Declare(services.GetRequiredService<AppSettingsViewModel>(), services.GetService<SyncViewModel>())),
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
