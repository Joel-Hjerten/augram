using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.UsedBy;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Augram.App.Hosting;

/// <summary>
/// The Ignored tab's slice of the composition root (F5 ignore list; plan 0001 M2 step 6; Global / Per command, plan 0004):
/// one <see cref="IgnoredViewModel"/> per sub-tab (keyed singletons, <see cref="IgnoreScope.Global"/> and
/// <see cref="IgnoreScope.PerCommand"/>), process-lifetime like the Commands tabs' (the selection survives tab switches), and
/// the tab entry with its two sub-tabs. Expects <see cref="MappingStore"/> and the form dialog and confirm presenters from the
/// engine and Commands registrations, and takes the Commands tab's <see cref="ICommandLocator"/> when there is one (a Per
/// command entry's "Used by" opens its commands through it); falls back to an empty mapping and presenters of its own when
/// they are absent (tests), never overriding a registration made before it. Register it after <see cref="CommandsModule"/>.
/// </summary>
public static class IgnoredModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => new MappingStore());
        services.TryAddSingleton<IFormDialogPresenter, FormDialogPresenter>();
        services.TryAddSingleton<IConfirmPresenter, ConfirmPresenter>();
        services.AddKeyedSingleton(IgnoreScope.Global, (sp, _) => Create(sp, IgnoreScope.Global));
        services.AddKeyedSingleton(IgnoreScope.PerCommand, (sp, _) => Create(sp, IgnoreScope.PerCommand));
        return services;
    }

    /// <summary>The Ignored tab with its Global and Per command sub-tabs; <c>AppNavigation.Build</c> puts it third. Without <see cref="Register"/> each says so instead of failing the window.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Ignored", AppNavigation.IgnoredKey, SubEntries:
        [
            SubEntry(services, "Global", AppNavigation.IgnoredGlobalKey, IgnoreScope.Global),
            SubEntry(services, "Per command", AppNavigation.IgnoredPerCommandKey, IgnoreScope.PerCommand),
        ]);
    }

    private static NavEntry SubEntry(IServiceProvider services, string title, string key, IgnoreScope scope)
        => new(title, key, () => services.GetKeyedService<IgnoredViewModel>(scope) is { } vm
            ? IgnoredScreen.Declare(vm)
            : new TextScreen(title, "The Ignored tab is not registered: CompositionRoot.Build needs IgnoredModule.Register(services) after CommandsModule.Register."));

    private static IgnoredViewModel Create(IServiceProvider sp, IgnoreScope scope) => new(
        sp.GetRequiredService<MappingStore>(),
        sp.GetRequiredService<IFormDialogPresenter>(),
        sp.GetRequiredService<IConfirmPresenter>(),
        CommandsModule.CurrentPlatform,
        scope,
        sp.GetService<ICommandLocator>());
}
