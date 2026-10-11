using Augram.App.Declarations;
using Augram.App.Dialogs;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Exclusions tab's slice of the composition root (F5 ignore list; plan 0001 M2 step 6; Global / Per command, plan 0004;
/// "Exclusions" in the UI since plan 0005, "Ignored" in the code):
/// one <see cref="IgnoredViewModel"/> per sub-tab (keyed singletons, <see cref="IgnoreScope.Global"/> and
/// <see cref="IgnoreScope.PerCommand"/>), process-lifetime like the Commands tabs' (the selection survives tab switches), and
/// the tab entry with its two sub-tabs. Resolves <see cref="MappingStore"/> (<see cref="ConfigModule"/>) and the form dialog
/// and confirm presenters (<see cref="DialogsModule"/>), and takes the Commands tab's <see cref="ICommandLocator"/> when there
/// is one (a Per command entry's "Used by" opens its commands through it).
/// </summary>
public static class IgnoredModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKeyedSingleton(IgnoreScope.Global, (sp, _) => Create(sp, IgnoreScope.Global));
        services.AddKeyedSingleton(IgnoreScope.PerCommand, (sp, _) => Create(sp, IgnoreScope.PerCommand));
        return services;
    }

    /// <summary>The tab's title: the UI word is "Exclusions" (plan 0005 decision 7, Joel 2026-10-10); the code keeps "Ignored".</summary>
    public const string Title = "Exclusions";

    /// <summary>
    /// The Exclusions tab with its Global and Per command sub-tabs; <c>AppNavigation.Build</c> puts it second. Without
    /// <see cref="Register"/> each says so instead of failing the window.
    /// </summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry(Title, AppNavigation.IgnoredKey, SubEntries:
        [
            SubEntry(services, "Global", AppNavigation.IgnoredGlobalKey, IgnoreScope.Global),
            SubEntry(services, "Per command", AppNavigation.IgnoredPerCommandKey, IgnoreScope.PerCommand),
        ]);
    }

    private static NavEntry SubEntry(IServiceProvider services, string title, string key, IgnoreScope scope)
        => new(title, key, () => services.GetKeyedService<IgnoredViewModel>(scope) is { } vm
            ? IgnoredScreen.Declare(vm)
            : new TextScreen(title, "The Exclusions tab is not registered: CompositionRoot.Build needs IgnoredModule.Register(services)."));

    private static IgnoredViewModel Create(IServiceProvider sp, IgnoreScope scope) => new(
        sp.GetRequiredService<MappingStore>(),
        sp.GetRequiredService<IFormDialogPresenter>(),
        sp.GetRequiredService<IConfirmPresenter>(),
        RunningPlatform.Current,
        scope,
        sp.GetService<ICommandLocator>());
}
