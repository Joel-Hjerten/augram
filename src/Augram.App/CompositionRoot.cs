using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.ViewModels;
using Augram.App.Views;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App;

/// <summary>
/// The single place where services are registered (ADR-0002 §2). Core stores, Engine services and
/// Platform adapters are added here as they land; nothing else in the app calls <c>new</c> on a service.
/// </summary>
internal static class CompositionRoot
{
    public static readonly TimeSpan HealthRefreshInterval = TimeSpan.FromSeconds(1);

    /// <param name="app">This build (version, commit, channel); <see cref="AppInfo.Current"/> when null.</param>
    /// <param name="logsFolder">Overrides <see cref="AppPaths.LogsFolder"/>; tests point it at a temp folder.</param>
    /// <param name="configFolder">Overrides <see cref="AppPaths.ConfigFolder"/>; tests point it at a temp folder so they never touch the real config.</param>
    /// <remarks>
    /// Building creates nothing: services come to life when first resolved. A launch that must first settle with another
    /// running Augram (<see cref="InstanceStartup"/>) resolves only <see cref="ITakeOverPresenter"/> until it may start.
    /// </remarks>
    public static ServiceProvider Build(AppInfo? app = null, string? logsFolder = null, string? configFolder = null)
    {
        var logs = logsFolder ?? AppPaths.LogsFolder;
        var services = new ServiceCollection();

        // Diagnostics (N4): one channel log feeding the in-memory tail and the rolling file; disposed with the provider.
        services.AddSingleton<InMemorySink>();
        services.AddSingleton(sp => new ChannelEventLog([sp.GetRequiredService<InMemorySink>(), new RollingFileSink(logs)]));
        services.AddSingleton<IEventLog>(sp => sp.GetRequiredService<ChannelEventLog>());
        services.AddSingleton<HealthRegistry>();
        services.AddSingleton<IHealthSource>(sp => sp.GetRequiredService<HealthRegistry>());
        services.AddSingleton<RecognitionLog>();
        services.AddSingleton<AppHealthContributor>();

        // App-level state and host services. The single-instance guard is not a service: Program owns it through InstanceStartup.
        services.AddSingleton(app ?? AppInfo.Current);
        services.AddSingleton<AppState>();
        services.AddSingleton<IClipboardText, AppClipboard>();
        services.AddSingleton<ITakeOverPresenter, TakeOverPresenter>();

        // Engine slice: config session and stores, Platform adapters, overlay, EngineHost (started in App.StartDesktop).
        EngineModule.Register(services, new EngineModuleOptions { ConfigFolder = configFolder });
        // Gestures tab slice: training session, presenters ("Used by…", delete confirmation), view model.
        GesturesModule.Register(services);
        // Commands tab slice, after the Gestures one: pickers, navigator, view model and the ICommandLocator the "Used by…" popup jumps through.
        CommandsModule.Register(services);
        // Sync slice (F8), after the engine's stores: git adapter, coordinator, the sync worker, dialogs, Options › Sync (started in App.StartDesktop).
        SyncModule.Register(services, new SyncModuleOptions { ConfigFolder = configFolder });

        // View models: projections over the stores above (ADR-0002 §5a).
        services.AddSingleton<AppSettingsViewModel>();
        services.AddSingleton(sp => new HealthViewModel(sp.GetRequiredService<IHealthSource>(), HealthRefreshInterval));
        services.AddSingleton(sp => new LogViewModel(sp.GetRequiredService<InMemorySink>(), logs, sp.GetRequiredService<IClipboardText>()));
        services.AddSingleton<RecognitionViewModel>();
        services.AddSingleton(AppNavigation.Build);
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
