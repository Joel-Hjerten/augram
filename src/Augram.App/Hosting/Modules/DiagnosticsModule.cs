using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Engine.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Diagnostics tab's slice of the composition root: the view models over the diagnostics services the root registers
/// first (the in-memory log tail, the health registry, the recognition log) and the tab entry with Health, Log and Recognition.
/// </summary>
public static class DiagnosticsModule
{
    /// <summary>How often Diagnostics › Health reads the health registry.</summary>
    public static readonly TimeSpan HealthRefreshInterval = TimeSpan.FromSeconds(1);

    /// <param name="services">The collection.</param>
    /// <param name="logsFolder">The rolling log's folder, which Diagnostics › Log opens.</param>
    public static IServiceCollection Register(IServiceCollection services, string logsFolder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logsFolder);
        services.AddSingleton(sp => new HealthViewModel(sp.GetRequiredService<IHealthSource>(), HealthRefreshInterval));
        services.AddSingleton(sp => new LogViewModel(sp.GetRequiredService<InMemorySink>(), logsFolder, sp.GetRequiredService<IClipboardText>()));
        services.AddSingleton<RecognitionViewModel>();
        return services;
    }

    /// <summary>The Diagnostics tab: Health, Log, Recognition; <c>AppNavigation.Build</c> puts it last before the dev gallery.</summary>
    public static NavEntry NavEntry(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new NavEntry("Diagnostics", AppNavigation.DiagnosticsKey, SubEntries:
        [
            new("Health", "diagnostics.health", () => HealthScreen.Declare(services.GetRequiredService<HealthViewModel>())),
            new("Log", "diagnostics.log", () => LogScreen.Declare(services.GetRequiredService<LogViewModel>())),
            new("Recognition", "diagnostics.recognition", () => RecognitionScreen.Declare(services.GetRequiredService<RecognitionViewModel>())),
        ]);
    }
}
