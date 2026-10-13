using Augram.App.Components.GesturePicker;
using Augram.App.Declarations;
using Augram.App.Dialogs;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Transfer;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Commands tab's slice of the composition root (plan 0001 M2; Global/Apps split 2026-10-07): the
/// gesture picker presenter, the tab navigator, the shared <see cref="CommandClipboard"/>,
/// one <see cref="CommandsViewModel"/> per sub-tab (keyed singletons, <see cref="CommandsScope.Global"/>
/// and <see cref="CommandsScope.Apps"/>), the <see cref="ICommandLocator"/> the Gestures tab jumps
/// through, and the tab entry. Resolves <see cref="MappingStore"/> and <see cref="GestureLibrary"/>
/// (<see cref="ConfigModule"/>), the training session (<see cref="GesturesModule"/>) and the form dialog and
/// confirm presenters (<see cref="DialogsModule"/>); the <see cref="IExportPresenter"/> and the
/// <see cref="SettingsStore"/> when they are there.
/// </summary>
public static class CommandsModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IGesturePickerPresenter, GesturePickerPresenter>();
        services.AddSingleton<TabNavigator>();
        services.AddSingleton<CommandClipboard>();
        services.AddKeyedSingleton(CommandsScope.Global, (sp, _) => Create(sp, CommandsScope.Global));
        services.AddKeyedSingleton(CommandsScope.Apps, (sp, _) => Create(sp, CommandsScope.Apps));
        services.AddSingleton<ICommandLocator>(sp => new CommandLocator(
            sp.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Global),
            sp.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Apps),
            sp.GetRequiredService<TabNavigator>()));
        return services;
    }

    /// <summary>
    /// The Commands tab with its Global and Apps sub-tabs; <c>AppNavigation.Build</c> puts it second.
    /// Without <see cref="Register"/> each sub-tab says so instead of failing the whole window.
    /// </summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Commands", AppNavigation.CommandsKey, SubEntries:
        [
            SubEntry(services, "Global", AppNavigation.CommandsGlobalKey, CommandsScope.Global),
            SubEntry(services, "Apps", AppNavigation.CommandsAppsKey, CommandsScope.Apps),
        ]);
    }

    private static NavEntry SubEntry(IServiceProvider services, string title, string key, CommandsScope scope)
        => new(title, key, () => services.GetKeyedService<CommandsViewModel>(scope) is { } vm
            ? CommandsScreen.Declare(vm)
            : new TextScreen(title, "The Commands tab is not registered: CompositionRoot.Build needs CommandsModule.Register(services)."));

    private static CommandsViewModel Create(IServiceProvider sp, CommandsScope scope)
        => new(
            scope,
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<IGesturePickerPresenter>(),
            sp.GetRequiredService<IFormDialogPresenter>(),
            sp.GetRequiredService<IConfirmPresenter>(),
            sp.GetRequiredService<CommandClipboard>(),
            RunningPlatform.Current,
            sp.GetService<IExportPresenter>(),
            sp.GetService<SettingsStore>(),
            sp.GetService<ISystemEvents>());
}
