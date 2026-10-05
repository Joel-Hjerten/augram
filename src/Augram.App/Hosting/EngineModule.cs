using System.Runtime.Versioning;
using Augram.App.Overlay;
using Augram.App.Training;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Engine.Hosting;
using Augram.Engine.Input;
using Augram.Platform.Windows.Input;
using Augram.Platform.Windows.Overlay;
using Augram.Platform.Windows.Startup;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting;

/// <summary>
/// The engine's slice of the composition root: config session and stores, the Platform adapters, the
/// overlay and the <see cref="EngineHost"/>, plus the links that keep them in step. <see cref="Register"/>
/// only registers; <see cref="Start"/> runs on the UI thread once Avalonia is up, shows the overlay and
/// starts the hook. Expects the diagnostics services (<see cref="IEventLog"/>, <see cref="HealthRegistry"/>,
/// <see cref="RecognitionLog"/>) to be registered already.
/// </summary>
public static class EngineModule
{
    public const string ConfigLogSource = "config";

    public static IServiceCollection Register(IServiceCollection services, EngineModuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= EngineModuleOptions.Default;
        var marshal = options.Marshal ?? (action => Dispatcher.UIThread.Post(action));

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(sp => CreateSession(sp, options, marshal));
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Settings);
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Gestures);
        services.AddSingleton(sp => options.InputSource?.Invoke(sp) ?? new SharpHookInputSource(sp.GetRequiredService<IClock>()));
        services.AddSingleton<IInputSimulator>(_ => new SharpHookInputSimulator());
        RegisterPlatform(services, options.PlatformAdapters);

        services.AddSingleton(sp => new TrailOverlayWindow(
            () => sp.GetRequiredService<SettingsStore>().Current.Trail,
            sp.GetRequiredService<IOverlayWindowStyle>(),
            sp.GetRequiredService<IEventLog>(),
            sp.GetService<HealthRegistry>()));
        services.AddSingleton<IStrokeTrail>(sp => sp.GetRequiredService<TrailOverlayWindow>());
        services.AddSingleton(CreateHost);
        services.AddSingleton<EngineSettingsLink>();
        services.AddSingleton(sp => new StrokeButtonDetection(sp.GetRequiredService<EngineHost>(), sp.GetRequiredService<SettingsStore>(), marshal));
        return services;
    }

    /// <summary>
    /// UI thread, after the framework initialised: creates the overlay (hidden until a stroke), links settings to the
    /// engine, installs the hook. Does nothing but log when <see cref="EngineKillSwitch"/> is set (<c>--no-engine</c>
    /// or <c>AUGRAM_NO_ENGINE=1</c>), so the UI can be run without touching the machine's input.
    /// </summary>
    public static void Start(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (EngineKillSwitch.IsSet())
        {
            services.GetRequiredService<IEventLog>().Info(LogSources.Engine, "Engine disabled by flag", ("argument", EngineKillSwitch.Argument), ("variable", EngineKillSwitch.EnvironmentVariable));
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            // Plan 0002: the macOS adapters (overlay style verification, window system, startup) do not exist yet.
            // Starting the hook here would show an overlay whose click-through state cannot be verified (invariant 6).
            services.GetRequiredService<IEventLog>().Info(LogSources.Engine, "Engine disabled: no platform adapters for this OS yet", ("os", Environment.OSVersion.Platform));
            return;
        }

        var host = services.GetRequiredService<EngineHost>();
        // A completed stroke that started over an open training canvas belongs to the training popup (F3), not to commands.
        if (services.GetService<ITrainingSession>() is { } training)
        {
            host.EventRaised += (_, e) => training.TryConsume(e);
        }

        services.GetRequiredService<EngineSettingsLink>();
        services.GetRequiredService<AppState>().SyncStartupRegistration();
        host.Start();
    }

    private static void RegisterPlatform(IServiceCollection services, bool adapters)
    {
        if (adapters && OperatingSystem.IsWindows())
        {
            RegisterWindows(services);
            return;
        }

        services.AddSingleton<IOverlayWindowStyle>(NullOverlayWindowStyle.Instance);
        services.AddSingleton<IStartupRegistration, NullStartupRegistration>();
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(IServiceCollection services)
    {
        services.AddSingleton<ICursorProbe, Win32CursorProbe>();
        services.AddSingleton<ISystemEvents, Win32SystemEvents>();
        services.AddSingleton<IOverlayWindowStyle, OverlayWindowStyle>();
        services.AddSingleton<IStartupRegistration>(_ => new RunKeyStartupRegistration());
    }

    private static ConfigSession CreateSession(IServiceProvider sp, EngineModuleOptions options, Action<Action> marshal)
    {
        var log = sp.GetRequiredService<IEventLog>();
        void Notice(string line)
        {
            if (line.StartsWith("Could not", StringComparison.Ordinal) || line.StartsWith("Settings reset", StringComparison.Ordinal) || line.Contains("skipped", StringComparison.Ordinal))
            {
                log.Warning(ConfigLogSource, line);
            }
            else
            {
                log.Info(ConfigLogSource, line);
            }
        }

        var store = new FileConfigStore(options.ConfigFolder ?? AppPaths.ConfigFolder, Notice);
        var scheduler = new TimerSaveScheduler(ConfigSession.SaveDelay, marshal);
        var session = new ConfigSession(store, scheduler.Schedule, Notice);
        log.Info(ConfigLogSource, "Configuration loaded", ("file", store.Location), ("gestures", session.Gestures.All.Count));
        if (!File.Exists(store.Location))
        {
            // First run: write the defaults and starter gestures now, so the file exists to look at and back up before any edit.
            session.Store.Save(session.Document);
            log.Info(ConfigLogSource, "Configuration created", ("file", store.Location));
        }

        return session;
    }

    private static EngineHost CreateHost(IServiceProvider sp)
    {
        var settings = sp.GetRequiredService<SettingsStore>();
        var gestures = sp.GetRequiredService<GestureLibrary>();
        var current = settings.Current;
        var ports = new EnginePorts
        {
            Input = sp.GetRequiredService<IInputSource>(),
            Simulator = sp.GetRequiredService<IInputSimulator>(),
            Clock = sp.GetRequiredService<IClock>(),
            Log = sp.GetRequiredService<IEventLog>(),
            Trail = sp.GetRequiredService<IStrokeTrail>(),
            RecognitionLog = sp.GetRequiredService<RecognitionLog>(),
            Health = sp.GetService<HealthRegistry>(),
            CursorProbe = sp.GetService<ICursorProbe>(),
            SystemEvents = sp.GetService<ISystemEvents>(),
        };
        var initial = new EngineHostOptions(
            current.General.StrokeButton,
            current.Capture,
            EngineSettingsLink.ToModifiers(current.General.IgnoreKey),
            current.General.Enabled);
        return new EngineHost(ports, () => gestures.All, () => settings.Current.Recognition, initial);
    }
}
