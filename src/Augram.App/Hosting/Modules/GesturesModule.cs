using Augram.App.Import;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Training;
using Augram.App.UsedBy;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Training;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Gestures tab's slice of the composition root (plan 0001 M1 step 7, M2 "Used by…"): the training session the engine
/// routes strokes to (its engine strokes reach the UI thread through <see cref="UiThread.RunOrPost"/>), the training options,
/// the presenters (training, the StrokesPlus.net import, "Used by…"), the view model and the tab entry. Resolves the stores
/// (<see cref="ConfigModule"/>), the confirmation (<see cref="DialogsModule"/>) and the file picker the import shares with
/// export (<see cref="TransferModule"/>). The <see cref="ICommandLocator"/> is optional and looked up when the popup opens, so
/// the Commands tab may register it after this module or not at all.
/// </summary>
public static class GesturesModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<Func<RecognitionOptions>>(sp => () => sp.GetService<SettingsStore>()?.Current.Recognition ?? RecognitionOptions.Default);
        services.AddSingleton(sp => new TrainingSession(
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<Func<RecognitionOptions>>(),
            sp.GetService<IEventLog>(),
            UiThread.RunOrPost));
        services.AddSingleton<ITrainingSession>(sp => sp.GetRequiredService<TrainingSession>());
        services.AddSingleton<ITrainingPresenter, TrainingPresenter>();
        services.AddSingleton<IImportPresenter, ImportPresenter>();
        services.AddSingleton<IUsedByPresenter>(sp => new UsedByPresenter(
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            () => sp.GetService<ICommandLocator>()));
        services.AddSingleton<GesturesViewModel>();
        return services;
    }

    /// <summary>The Gestures tab; <c>AppNavigation.Build</c> puts it after Commands and Exclusions.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Gestures", AppNavigation.GesturesKey, () => GesturesScreen.Declare(services.GetRequiredService<GesturesViewModel>()));
    }
}
