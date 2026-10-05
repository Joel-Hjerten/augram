using Augram.App.Import;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Training;
using Augram.App.ViewModels;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Augram.App.Hosting;

/// <summary>
/// The Gestures tab's slice of the composition root (plan 0001 M1 step 7): the training session the
/// engine routes strokes to, the two presenters, the view model and the tab entry. Expects
/// <see cref="GestureLibrary"/>, <see cref="SettingsStore"/> and <see cref="Core.Abstractions.IEventLog"/>
/// from the config and engine registrations; falls back to the starter set and default options when
/// they are absent (tests, gallery), never overriding a registration made before it.
/// </summary>
public static class GesturesModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => new GestureLibrary(StarterGestures.All()));
        services.TryAddSingleton<Func<RecognitionOptions>>(sp => () => sp.GetService<SettingsStore>()?.Current.Recognition ?? RecognitionOptions.Default);
        services.AddSingleton<TrainingSession>();
        services.AddSingleton<ITrainingSession>(sp => sp.GetRequiredService<TrainingSession>());
        services.AddSingleton<ITrainingPresenter, TrainingPresenter>();
        services.AddSingleton<IImportPresenter, ImportPresenter>();
        services.AddSingleton<GesturesViewModel>();
        return services;
    }

    /// <summary>The Gestures tab; <c>AppNavigation.Build</c> puts it first.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Gestures", AppNavigation.GesturesKey, () => GesturesScreen.Declare(services.GetRequiredService<GesturesViewModel>()));
    }
}
