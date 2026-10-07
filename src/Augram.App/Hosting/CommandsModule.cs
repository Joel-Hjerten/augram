using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Training;
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
/// The Commands tab's slice of the composition root (plan 0001 M2): the gesture picker and form
/// dialog presenters, the tab navigator, the view model, the <see cref="ICommandLocator"/> the
/// Gestures tab jumps through, and the tab entry. Expects <see cref="MappingStore"/>,
/// <see cref="GestureLibrary"/>, the training session and the <see cref="IConfirmPresenter"/> from
/// the config, engine and Gestures registrations; falls back to an empty mapping, the starter
/// gestures and presenters of its own when they are absent (tests, gallery), never overriding a
/// registration made before it. Register it after <see cref="GesturesModule"/>.
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
        services.AddSingleton(sp => new CommandsViewModel(
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<IGesturePickerPresenter>(),
            sp.GetRequiredService<IFormDialogPresenter>(),
            sp.GetRequiredService<IConfirmPresenter>(),
            CurrentPlatform));
        services.AddSingleton<ICommandLocator, CommandLocator>();
        return services;
    }

    /// <summary>The Commands tab; <c>AppNavigation.Build</c> puts it second. Without <see cref="Register"/> the tab says so instead of failing the whole window.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Commands", AppNavigation.CommandsKey, () => services.GetService<CommandsViewModel>() is { } vm
            ? CommandsScreen.Declare(vm)
            : new TextScreen("Commands", "The Commands tab is not registered: CompositionRoot.Build needs CommandsModule.Register(services) after GesturesModule.Register."));
    }
}
