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
/// The Ignored tab's slice of the composition root (F5 ignore list; plan 0001 M2 step 6): its view model, a process-lifetime
/// singleton like the Commands tabs' (the selection survives tab switches), and the tab entry. Expects
/// <see cref="MappingStore"/> and the form dialog and confirm presenters from the engine and Commands registrations; falls
/// back to an empty mapping and presenters of its own when they are absent (tests), never overriding a registration made
/// before it. Register it after <see cref="CommandsModule"/>.
/// </summary>
public static class IgnoredModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => new MappingStore());
        services.TryAddSingleton<IFormDialogPresenter, FormDialogPresenter>();
        services.TryAddSingleton<IConfirmPresenter, ConfirmPresenter>();
        services.AddSingleton(sp => new IgnoredViewModel(
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<IFormDialogPresenter>(),
            sp.GetRequiredService<IConfirmPresenter>(),
            CommandsModule.CurrentPlatform));
        return services;
    }

    /// <summary>The Ignored tab; <c>AppNavigation.Build</c> puts it third. Without <see cref="Register"/> it says so instead of failing the window.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Ignored", AppNavigation.IgnoredKey, () => services.GetService<IgnoredViewModel>() is { } vm
            ? IgnoredScreen.Declare(vm)
            : new TextScreen("Ignored", "The Ignored tab is not registered: CompositionRoot.Build needs IgnoredModule.Register(services) after CommandsModule.Register."));
    }
}
