using Augram.App.Hosting.Launch;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The configuration's slice of the composition root: the <see cref="ConfigSession"/> over a <see cref="FileConfigStore"/> in
/// the config folder and its three stores (<see cref="SettingsStore"/>, <see cref="Core.Gestures.GestureLibrary"/>,
/// <see cref="MappingStore"/>). Everything else reads them: the engine, sync, transfer, every tab, and a <c>--no-engine</c> run
/// too. Loading logs as source <c>config</c>; a first run writes the defaults and starter gestures at once; saves are
/// debounced by a <see cref="OneShotTimer"/> whose action is marshalled to the UI thread, so the session stays single-writer;
/// an earlier StrokesPlus.net import's placeholders are upgraded once (<see cref="UpgradeImportedSteps"/>). The session is
/// created when first resolved; disposing the provider flushes a pending save. Expects <see cref="IEventLog"/>.
/// </summary>
public static class ConfigModule
{
    public static IServiceCollection Register(IServiceCollection services, ConfigModuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= ConfigModuleOptions.Default;
        var marshal = options.Marshal ?? UiThread.Post;

        services.AddSingleton(sp => CreateSession(sp, options, marshal));
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Settings);
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Gestures);
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Mapping);
        return services;
    }

    private static ConfigSession CreateSession(IServiceProvider sp, ConfigModuleOptions options, Action<Action> marshal)
    {
        var log = sp.GetRequiredService<IEventLog>();
        void Notice(string line)
        {
            if (line.StartsWith("Could not", StringComparison.Ordinal) || line.StartsWith("Settings reset", StringComparison.Ordinal) || line.Contains("skipped", StringComparison.Ordinal))
            {
                log.Warning(LogSources.Config, line);
            }
            else
            {
                log.Info(LogSources.Config, line);
            }
        }

        var store = new FileConfigStore(options.ConfigFolder ?? AppPaths.ConfigFolder, Notice);
        var timer = new OneShotTimer(marshal);
        var session = new ConfigSession(store, action => timer.Schedule(ConfigSession.SaveDelay, action), Notice);
        log.Info(LogSources.Config, "Configuration loaded", ("file", store.Location), ("gestures", session.Gestures.All.Count));
        UpgradeImportedSteps(session, log);
        if (!File.Exists(store.Location))
        {
            // First run: write the defaults and starter gestures now, so the file exists to look at and back up before any edit.
            session.Store.Save(session.Document);
            log.Info(LogSources.Config, "Configuration created", ("file", store.Location));
        }

        return session;
    }

    /// <summary>
    /// The placeholders an earlier StrokesPlus.net import saved, as the step types that exist now (<see cref="PlaceholderUpgrade"/>):
    /// once per new step type, a no-op on every later start. Committed through the mapping store and written at once, so
    /// sync publishes it like any edit; not undoable, since it is not the user's edit.
    /// </summary>
    private static void UpgradeImportedSteps(ConfigSession session, IEventLog log)
    {
        var upgrade = PlaceholderUpgrade.Upgrade(session.Mapping.Current);
        if (upgrade.Count == 0)
        {
            return;
        }

        session.Mapping.ReplaceAll(upgrade.Mapping);
        session.Mapping.ClearHistory();
        session.Flush();
        var methods = string.Join(", ", upgrade.Methods.Select(method => $"{method.Key} {method.Value}"));
        log.Info(LogSources.Config, "Imported steps upgraded", ("steps", upgrade.Count), ("methods", methods));
    }
}
