using Augram.App.Sync;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Sync.Git;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Augram.App.Hosting;

/// <summary>
/// The sync slice of the composition root (F8 sync): <see cref="SyncFolders"/> under the config folder, the git
/// adapter (<see cref="GitSyncRepository"/> on <c>sync/repo</c>) as <see cref="ISyncRepository"/>, the
/// <see cref="SyncBaseStore"/>, the <see cref="SyncStoreThread"/>, the <see cref="SyncCoordinator"/> over the config
/// session's stores, the <see cref="SyncService"/> (the worker), the join and conflict presenters, the prompter, the
/// health contributor and the Options › Sync view model. Expects the stores, <see cref="IEventLog"/> and
/// <see cref="IClock"/> from <see cref="EngineModule"/>; register it after that one. <see cref="Register"/> only
/// registers; <see cref="Start"/> runs on the UI thread once Avalonia is up, also under <c>--no-engine</c> (sync never
/// touches input), and starts the worker, whose first run is the start-up sync. Disposal runs in reverse through the
/// service provider: the service stops before the config session flushes.
/// </summary>
public static class SyncModule
{
    public static IServiceCollection Register(IServiceCollection services, SyncModuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= SyncModuleOptions.Default;

        services.AddSingleton(sp => new SyncFolders(options.ConfigFolder ?? AppPaths.ConfigFolder, Clock(sp)));
        services.AddSingleton(sp =>
        {
            var folder = sp.GetRequiredService<SyncFolders>().RepositoryFolder;
            return options.Repository?.Invoke(folder)
                ?? new GitSyncRepository(folder, machineLabel: sp.GetRequiredService<SettingsStore>().Current.Sync.MachineName);
        });
        services.AddSingleton(sp =>
        {
            var log = sp.GetRequiredService<IEventLog>();
            return new SyncBaseStore(sp.GetRequiredService<SyncFolders>().ConfigFolder, line => log.Warning(SyncService.LogSource, line));
        });
        services.AddSingleton(_ => new SyncStoreThread(options.StoreThread));
        services.AddSingleton(sp => new SyncCoordinator(
            sp.GetRequiredService<ISyncRepository>(),
            sp.GetRequiredService<SettingsStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<SyncBaseStore>(),
            Clock(sp),
            sp.GetRequiredService<IEventLog>(),
            sp.GetRequiredService<SyncStoreThread>().Run));
        services.AddSingleton(sp => new SyncService(
            sp.GetRequiredService<SyncCoordinator>(),
            sp.GetRequiredService<SettingsStore>(),
            sp.GetRequiredService<GestureLibrary>(),
            sp.GetRequiredService<MappingStore>(),
            sp.GetRequiredService<SyncBaseStore>(),
            sp.GetRequiredService<SyncFolders>(),
            sp.GetRequiredService<SyncStoreThread>(),
            Clock(sp),
            sp.GetRequiredService<IEventLog>(),
            options.Schedule));
        services.TryAddSingleton<ISyncJoinPresenter, SyncJoinPresenter>();
        services.TryAddSingleton<ISyncConflictPresenter>(sp => new SyncConflictPresenter(sp.GetRequiredService<GestureLibrary>(), sp.GetRequiredService<MappingStore>()));
        services.AddSingleton(sp => new SyncPrompter(sp.GetRequiredService<SyncService>(), sp.GetRequiredService<ISyncJoinPresenter>(), options.Marshal));
        services.AddSingleton(sp => new SyncViewModel(
            sp.GetRequiredService<SettingsStore>(),
            sp.GetRequiredService<SyncService>(),
            sp.GetRequiredService<ISyncConflictPresenter>(),
            options.Marshal));
        services.AddSingleton(sp => new SyncHealthContributor(sp.GetRequiredService<HealthRegistry>(), sp.GetRequiredService<SyncService>()));
        return services;
    }

    /// <summary>UI thread, after the framework initialised: the prompter and the health row listen, then the worker starts with the start-up run (when a repository is set and automatic sync is on).</summary>
    public static void Start(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetRequiredService<SyncPrompter>();
        services.GetRequiredService<SyncHealthContributor>();
        var service = services.GetRequiredService<SyncService>();
        services.GetRequiredService<IEventLog>().Info(
            SyncService.LogSource,
            "Sync ready",
            ("repository", SyncSettingsRules.Host(services.GetRequiredService<SettingsStore>().Current.Sync.RepositoryUrl)),
            ("automatic", services.GetRequiredService<SettingsStore>().Current.Sync.AutoSync));
        service.Start();
    }

    private static IClock Clock(IServiceProvider sp) => sp.GetService<IClock>() ?? SystemClock.Instance;
}
