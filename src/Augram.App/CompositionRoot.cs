using Augram.App.Dialogs;
using Augram.App.Hosting;
using Augram.App.Hosting.Launch;
using Augram.App.Hosting.Modules;
using Augram.App.Hosting.Startup;
using Augram.App.Navigation;
using Augram.App.Sync;
using Augram.App.Tray;
using Augram.App.ViewModels;
using Augram.App.Views;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App;

/// <summary>
/// The single place where services are registered (ADR-0002 §2): the diagnostics, the launch's state, then one module per
/// slice (<c>Hosting/Modules/</c>), then the tray and the window. Nothing else in the app calls <c>new</c> on a service. The
/// order things are built, started and disposed in is in <c>Hosting/README.md</c>.
/// </summary>
internal static class CompositionRoot
{
    /// <param name="app">This build (version, commit, channel); <see cref="AppInfo.Current"/> when null.</param>
    /// <param name="logsFolder">Overrides <see cref="AppPaths.LogsFolder"/>; tests point it at a temp folder.</param>
    /// <param name="configFolder">Overrides <see cref="AppPaths.ConfigFolder"/>; tests point it at a temp folder so they never touch the real config.</param>
    /// <remarks>
    /// Building creates nothing: services come to life when first resolved. A launch that must first settle with another
    /// running Augram (<see cref="Hosting.Instance.InstanceStartup"/>) resolves only the take-over's <see cref="IConfirmPresenter"/> (keyed <see cref="ConfirmPresenter.AloneKey"/>) until it may start.
    /// </remarks>
    public static ServiceProvider Build(AppInfo? app = null, string? logsFolder = null, string? configFolder = null)
    {
        var logs = logsFolder ?? AppPaths.LogsFolder;
        var services = new ServiceCollection();
        // The one UI-thread marshal: every module, and every service built here, that hands work to the UI thread gets this one.
        Action<Action> ui = UiThread.Post;

        // Diagnostics (N4): one channel log feeding the in-memory tail and the rolling file; disposed with the provider.
        services.AddSingleton<InMemorySink>();
        services.AddSingleton(sp => new ChannelEventLog([sp.GetRequiredService<InMemorySink>(), new RollingFileSink(logs)]));
        services.AddSingleton<IEventLog>(sp => sp.GetRequiredService<ChannelEventLog>());
        services.AddSingleton<HealthRegistry>();
        services.AddSingleton<IHealthSource>(sp => sp.GetRequiredService<HealthRegistry>());
        services.AddSingleton<RecognitionLog>();
        services.AddSingleton<AppHealthContributor>();

        // The launch and app-level state. The single-instance listener is not a service: Program owns it through InstanceStartup.
        services.AddSingleton(app ?? AppInfo.Current);
        services.AddSingleton(_ => LaunchArguments.Current);
        services.AddSingleton<StartAtLoginLink>();
        services.AddSingleton<AppState>();
        services.AddSingleton<IClipboardText, AppClipboard>();

        // The dialogs every slice shares, the take-over's confirmation among them.
        DialogsModule.Register(services);
        // The config session and its stores, the Platform adapters, then the engine over both (started in App.StartDesktop).
        ConfigModule.Register(services, new ConfigModuleOptions { ConfigFolder = configFolder, Marshal = ui });
        PlatformModule.Register(services);
        EngineModule.Register(services, new EngineModuleOptions { Marshal = ui });
        // The tabs: Gestures (training, "Used by…"), Commands (pickers, navigator, the locator the "Used by…" popup jumps through), Exclusions.
        GesturesModule.Register(services);
        CommandsModule.Register(services);
        IgnoredModule.Register(services);
        // Export and import of Augram files (plan 0003): presenters, file picker, Options › Sync › Export and import.
        TransferModule.Register(services);
        // Sync (F8): git adapter, coordinator, the sync worker, dialogs, Options › Sync (started in App.StartDesktop).
        SyncModule.Register(services, new SyncModuleOptions { ConfigFolder = configFolder, Marshal = ui });
        OptionsModule.Register(services);
        DiagnosticsModule.Register(services, logs);

        // The tray (resolved in App.StartDesktop once the engine and sync started): Open shows the window, Quit ends the lifetime.
        services.AddSingleton(sp => new AppTray(
            sp.GetRequiredService<AppState>(),
            sp.GetRequiredService<MainWindowOpener>().ShowNow,
            () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(),
            sp.GetRequiredService<IEventLog>(),
            sp.GetService<SyncService>(),
            sp.GetService<EnginePauseState>()));

        // The window: the tabs (each from its module's NavEntry) and the shell's view model.
        services.AddSingleton(AppNavigation.Build);
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
