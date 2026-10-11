using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Options tab's slice of the composition root: <see cref="AppSettingsViewModel"/> (General, Strokes, Appearance, About)
/// and the tab entry with its five sub-tabs (plan 0006 decision 9). Options › Sync shows <see cref="SyncViewModel"/> and
/// <see cref="ConfigurationViewModel"/> when <see cref="SyncModule"/> and <see cref="TransferModule"/> registered them, and
/// what it has otherwise.
/// </summary>
public static class OptionsModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<AppSettingsViewModel>();
        return services;
    }

    /// <summary>The Options tab: General, Strokes, Appearance, Sync, About; <c>AppNavigation.Build</c> puts it after Gestures.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AppSettingsViewModel Settings() => services.GetRequiredService<AppSettingsViewModel>();
        return new NavEntry("Options", AppNavigation.OptionsKey, SubEntries:
        [
            new(OptionsScreen.GeneralTitle, AppNavigation.OptionsGeneralKey, () => OptionsScreen.DeclareGeneral(Settings())),
            new(OptionsScreen.StrokesTitle, AppNavigation.OptionsStrokesKey, () => OptionsScreen.DeclareStrokes(Settings())),
            new(OptionsScreen.AppearanceTitle, AppNavigation.OptionsAppearanceKey, () => OptionsScreen.DeclareAppearance(Settings())),
            new(OptionsScreen.SyncTitle, AppNavigation.OptionsSyncKey, () => OptionsScreen.DeclareSync(services.GetService<SyncViewModel>(), services.GetService<ConfigurationViewModel>())),
            new(OptionsScreen.AboutTitle, AppNavigation.OptionsAboutKey, () => OptionsScreen.DeclareAbout(Settings())),
        ]);
    }
}
