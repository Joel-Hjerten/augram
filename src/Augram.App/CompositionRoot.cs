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

    /// <param name="guard">The process's single-instance guard; null in tests.</param>
    /// <param name="logsFolder">Overrides <see cref="AppPaths.LogsFolder"/>; tests point it at a temp folder.</param>
    public static ServiceProvider Build(SingleInstanceGuard? guard = null, string? logsFolder = null)
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

        // App-level state and host services.
        services.AddSingleton<AppState>();
        services.AddSingleton<IClipboardText, AppClipboard>();
        if (guard is not null)
        {
            services.AddSingleton(guard);
        }

        // EngineHost registration lands in step 4b (hook adapter, worker, capture, recognition feeding RecognitionLog).

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
