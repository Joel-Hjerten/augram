using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Training;
using Augram.App.Transfer;
using Augram.App.UsedBy;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Augram.App.Hosting;

/// <summary>
/// The Commands tab's slice of the composition root (plan 0001 M2; Global/Apps split 2026-10-07): the
/// gesture picker and form dialog presenters, the tab navigator, the shared <see cref="CommandClipboard"/>,
/// one <see cref="CommandsViewModel"/> per sub-tab (keyed singletons, <see cref="CommandsScope.Global"/>
/// and <see cref="CommandsScope.Apps"/>), the <see cref="ICommandLocator"/> the Gestures tab jumps
/// through, and the tab entry. Expects <see cref="MappingStore"/>, <see cref="GestureLibrary"/>, the
/// training session and the <see cref="IConfirmPresenter"/> from the config, engine and Gestures
/// registrations; falls back to an empty mapping, the starter gestures and presenters of its own when
/// they are absent (tests, gallery), never overriding a registration made before it. Register it after
/// <see cref="GesturesModule"/>.
/// </summary>
public static class CommandsModule
{
    /// <summary>The platform a step authored on this machine is stored with (F8).</summary>
    public static HostPlatform CurrentPlatform => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => new MappingStore());
        services.TryAddSingleton(_ => new GestureLibrary(StarterGestures.All()));
        services.TryAddSingleton<Func<RecognitionOptions>>(sp => () => sp.GetService<SettingsStore>()?.Current.Recognition ?? RecognitionOptions.Default);
        services.TryAddSingleton<TrainingSession>();
        services.TryAddSingleton<ITrainingPresenter, TrainingPresenter>();
        services.TryAddSingleton<IConfirmPresenter, ConfirmPresenter>();
        services.AddSingleton<IGesturePickerPresenter, GesturePickerPresenter>();
        services.AddSingleton<IFormDialogPresenter, FormDialogPresenter>();
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
            : new TextScreen(title, "The Commands tab is not registered: CompositionRoot.Build needs CommandsModule.Register(services) after GesturesModule.Register."));

    private static CommandsViewModel Create(IServiceProvider sp, CommandsScope scope)
    {
        var vm = new CommandsViewModel(
            scope,
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<IGesturePickerPresenter>(),
            sp.GetRequiredService<IFormDialogPresenter>(),
            sp.GetRequiredService<IConfirmPresenter>(),
            sp.GetRequiredService<CommandClipboard>(),
            CurrentPlatform,
            sp.GetService<IExportPresenter>());
        if (sp.GetService<SettingsStore>() is { } settings)
        {
            // This machine's stroke button: a trigger naming it means the stroke button here, which the header says.
            vm.StrokeButton = settings.Current.General.StrokeButton;
            settings.Changed += (_, _) => vm.StrokeButton = settings.Current.General.StrokeButton;
        }

        return vm;
    }
}
