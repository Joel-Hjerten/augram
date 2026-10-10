using System.Runtime.Versioning;
using Augram.App.Components.HotkeyCapture;
using Augram.App.Components.Steps.DisplayMode;
using Augram.App.Components.WindowFinder;
using Augram.App.Overlay;
using Augram.App.Training;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using Augram.Engine.Input;
using Augram.Import.StrokesPlus;
using Augram.Platform.MacOS;
using Augram.Platform.MacOS.Clipboard;
using Augram.Platform.MacOS.Display;
using Augram.Platform.MacOS.Input;
using Augram.Platform.MacOS.Launch;
using Augram.Platform.MacOS.Overlay;
using Augram.Platform.MacOS.Startup;
using Augram.Platform.MacOS.WindowSystem;
using Augram.Platform.Windows.Apps;
using Augram.Platform.Windows.Clipboard;
using Augram.Platform.Windows.Display;
using Augram.Platform.Windows.Input;
using Augram.Platform.Windows.Launch;
using Augram.Platform.Windows.Overlay;
using Augram.Platform.Windows.Startup;
using Augram.Platform.Windows.WindowSystem;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting;

/// <summary>
/// The engine's slice of the composition root: config session and stores (settings, gestures, mapping),
/// the Platform adapters (window system and window operations for the M2 executor among them), the
/// overlay and the <see cref="EngineHost"/>, plus the links that keep them in step. <see cref="Register"/>
/// only registers; <see cref="Start"/> runs on the UI thread once Avalonia is up, shows the overlay and
/// starts the hook. Expects the diagnostics services (<see cref="IEventLog"/>, <see cref="HealthRegistry"/>,
/// <see cref="RecognitionLog"/>) to be registered already. The host's <see cref="EnginePorts.Intercept"/>
/// is the training session when one is registered later in the root (<see cref="GesturesModule"/>); it is
/// resolved when the host is created, not when the module is registered, so the order of the two modules does
/// not matter. <see cref="EngineHostOptions.SettleDelayMs"/> stays the default: there is no setting for it yet.
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
        services.AddSingleton(sp => sp.GetRequiredService<ConfigSession>().Mapping);
        // Shared by the hook and the simulator: the hook takes other programs' wheel events but not our own Scroll step's.
        services.AddSingleton(_ => new OwnWheelInjections());
        services.AddSingleton(sp => options.InputSource?.Invoke(sp) ?? new SharpHookInputSource(sp.GetRequiredService<IClock>(), sp.GetRequiredService<IEventLog>(), sp.GetRequiredService<OwnWheelInjections>()));
        services.AddSingleton(sp => CreateSimulator(sp.GetRequiredService<OwnWheelInjections>(), options.PlatformAdapters));
        RegisterPlatform(services, options.PlatformAdapters);

        services.AddSingleton(sp => new TrailOverlayWindow(
            () => sp.GetRequiredService<SettingsStore>().Current.Trail,
            sp.GetRequiredService<IOverlayWindowStyle>(),
            sp.GetRequiredService<IEventLog>(),
            sp.GetService<HealthRegistry>()));
        services.AddSingleton(sp => new NativeTrailOverlay(
            () => sp.GetRequiredService<SettingsStore>().Current.Trail,
            sp.GetRequiredService<ITrailSurface>(),
            sp.GetRequiredService<IEventLog>(),
            sp.GetService<HealthRegistry>()));
        // A platform with its own trail surface (macOS: a panel that shows over full-screen apps) draws there; otherwise the Avalonia window.
        services.AddSingleton<IStrokeTrail>(sp => sp.GetService<ITrailSurface>() is null
            ? sp.GetRequiredService<TrailOverlayWindow>()
            : sp.GetRequiredService<NativeTrailOverlay>());
        services.AddSingleton(CreateHost);
        services.AddSingleton<EngineSettingsLink>();
        services.AddSingleton(_ => new EnginePauseState(marshal));
        services.AddSingleton(sp => new StrokeButtonDetection(sp.GetRequiredService<EngineHost>(), sp.GetRequiredService<SettingsStore>(), marshal));
        services.AddSingleton<IKeyCapture>(sp => new EngineKeyCapture(sp.GetRequiredService<EngineHost>(), marshal));
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
        // Reading display modes or the window under the pointer changes nothing, so the forms get them under --no-engine too.
        PublishReadOnlyAdapters(services);
        if (EngineKillSwitch.IsSet())
        {
            services.GetRequiredService<IEventLog>().Info(LogSources.Engine, "Engine disabled by flag", ("argument", EngineKillSwitch.Argument), ("variable", EngineKillSwitch.EnvironmentVariable));
            return;
        }

        // A completed stroke that started over an open training canvas belongs to the training popup (F3, A6), not
        // to commands. The host asks through its Intercept port (wired in BuildPorts) before executing anything, so
        // a claimed stroke never fires a command; nothing is subscribed to EventRaised for it any more.
        if (OperatingSystem.IsMacOS())
        {
            LogAccessibility(services.GetRequiredService<IEventLog>());
        }

        var host = services.GetRequiredService<EngineHost>();
        services.GetRequiredService<EngineSettingsLink>();
        services.GetRequiredService<AppState>().SyncStartupRegistration();
        host.Start();
        // The tray says "paused: VMware is focused" while a "disable while focused" app has focus (F5 ignore list).
        services.GetRequiredService<EnginePauseState>().Follow(host);
        PublishKeyCapture(services);
    }

    /// <summary>The hook, injected input and window operations all need the Accessibility permission; without it they fail one by one, so say it once up front.</summary>
    [SupportedOSPlatform("macos")]
    private static void LogAccessibility(IEventLog log)
    {
        if (MacAccessibility.IsTrusted())
        {
            log.Info(LogSources.Engine, "Accessibility permission granted");
        }
        else
        {
            log.Warning(LogSources.Engine, "Accessibility permission missing; the hook and window operations fail until it is granted", ("settings", "Privacy & Security > Accessibility"));
        }
    }

    /// <summary>
    /// Makes the engine's key capture reachable from every hotkey field (F5): step forms are built by
    /// <c>StepFormRegistry</c> without services, so the field looks the capture up as an application resource.
    /// Only a started engine publishes it; under <c>--no-engine</c> the field falls back to its window's keys.
    /// </summary>
    internal static void PublishKeyCapture(IServiceProvider services)
    {
        if (Application.Current is { } app)
        {
            app.Resources[HotkeyCaptureBox.KeyCaptureResourceKey] = services.GetRequiredService<IKeyCapture>();
        }
    }

    /// <summary>
    /// The adapters forms only read, for forms built without services like the key capture above: the display modes for the
    /// Display mode step form (<c>StepFormRegistry</c>), the window system for the window finder's magnifiers.
    /// </summary>
    internal static void PublishReadOnlyAdapters(IServiceProvider services)
    {
        if (Application.Current is { } app)
        {
            app.Resources[DisplayModeStepForm.DisplayModesResourceKey] = services.GetRequiredService<IDisplayModes>();
            app.Resources[WindowFinder.WindowSystemResourceKey] = services.GetRequiredService<IWindowSystem>();
        }
    }

    /// <summary>
    /// The host's ports as the composition root resolves them: the input source and simulator, the diagnostics,
    /// the overlay, the platform adapters, the mapping snapshot delegate for the executor and the training
    /// session as <see cref="EnginePorts.Intercept"/> (null when no <see cref="ITrainingSession"/> is registered).
    /// Separate from <see cref="CreateHost"/> so tests can check the wiring without a host.
    /// </summary>
    internal static EnginePorts BuildPorts(IServiceProvider sp)
    {
        var mapping = sp.GetRequiredService<MappingStore>();
        var training = sp.GetService<ITrainingSession>();
        return new EnginePorts
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
            Windows = sp.GetRequiredService<IWindowSystem>(),
            WindowOperations = sp.GetRequiredService<IWindowOperations>(),
            ProcessLauncher = sp.GetRequiredService<IProcessLauncher>(),
            DisplayModes = sp.GetRequiredService<IDisplayModes>(),
            Clipboard = sp.GetRequiredService<IClipboard>(),
            Apps = sp.GetRequiredService<IAppActivator>(),
            AppWindow = sp.GetRequiredService<MainWindowOpener>(),
            Mapping = () => mapping.Current,
            Intercept = training is null ? null : e => training.TryConsume(e),
        };
    }

    /// <summary>
    /// SharpHook's simulator; on macOS wrapped so the volume and playback keys reach the system (<see cref="MacMediaKeySimulator"/>)
    /// and a hold remap's button output, its modifiers and its re-posted drags go through CoreGraphics (<see cref="MacRemapButtonSimulator"/>).
    /// </summary>
    private static IInputSimulator CreateSimulator(OwnWheelInjections ownWheel, bool adapters)
    {
        var simulator = new SharpHookInputSimulator(ownWheel);
        return adapters && OperatingSystem.IsMacOS() ? new MacRemapButtonSimulator(new MacMediaKeySimulator(simulator)) : simulator;
    }

    private static void RegisterPlatform(IServiceCollection services, bool adapters)
    {
        services.AddSingleton<MainWindowOpener>();
        services.AddSingleton<IAppWindow>(sp => sp.GetRequiredService<MainWindowOpener>());
        if (adapters && OperatingSystem.IsWindows())
        {
            RegisterWindows(services);
            return;
        }

        if (adapters && OperatingSystem.IsMacOS())
        {
            RegisterMacOS(services);
            return;
        }

        services.AddSingleton<IOverlayWindowStyle>(NullOverlayWindowStyle.Instance);
        services.AddSingleton<IStartupRegistration, NullStartupRegistration>();
        services.AddSingleton<IWindowSystem>(NullWindowSystem.Instance);
        services.AddSingleton<IWindowOperations>(NullWindowOperations.Instance);
        services.AddSingleton<IProcessLauncher>(NullProcessLauncher.Instance);
        services.AddSingleton<IDisplayModes>(NullDisplayModes.Instance);
        services.AddSingleton<IClipboard>(NullClipboard.Instance);
        services.AddSingleton<IAppActivator>(NullAppActivator.Instance);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(IServiceCollection services)
    {
        services.AddSingleton<ICursorProbe, Win32CursorProbe>();
        services.AddSingleton<ISystemEvents, Win32SystemEvents>();
        services.AddSingleton<IOverlayWindowStyle, OverlayWindowStyle>();
        // The Run entry carries --hidden, so a login launch starts in the tray (LaunchVisibility).
        services.AddSingleton<IStartupRegistration>(_ => new RunKeyStartupRegistration(LaunchVisibility.HiddenArgument));
        // Constructing these touches no window and installs nothing; every call they make runs on the engine worker.
        services.AddSingleton<IWindowSystem>(sp => new Win32WindowSystem(sp.GetRequiredService<IEventLog>()));
        services.AddSingleton<IWindowOperations>(_ => new Win32WindowOperations());
        services.AddSingleton<IProcessLauncher>(sp => new Win32ProcessLauncher(sp.GetRequiredService<IEventLog>()));
        services.AddSingleton<IDisplayModes>(_ => new Win32DisplayModes());
        services.AddSingleton<IClipboard>(_ => new Win32Clipboard());
        services.AddSingleton<IAppActivator>(sp => new Win32AppActivator(sp.GetRequiredService<IEventLog>()));
    }

    /// <summary>
    /// The macOS adapters: Accessibility-API window system and operations (close, minimize, maximize/restore so far), a
    /// verified click-through overlay (the trail's own panel, <see cref="MacTrailPanel"/>), the cursor probe, and the system
    /// events (<see cref="MacSystemEvents"/>: a new frontmost app for hold remaps, sleep and wake, lock and unlock; resolved
    /// with the host in <see cref="Start"/>, on the main thread, where its observers must be registered), and start at login as
    /// a login item (<see cref="MacLoginItemRegistration"/>, SMAppService).
    /// </summary>
    [SupportedOSPlatform("macos")]
    private static void RegisterMacOS(IServiceCollection services)
    {
        services.AddSingleton<ICursorProbe, MacCursorProbe>();
        services.AddSingleton<ISystemEvents, MacSystemEvents>();
        // The trail is the panel; no IOverlayWindowStyle, so the Avalonia trail window cannot be built here by mistake.
        services.AddSingleton<ITrailSurface, MacTrailPanel>();
        services.AddSingleton<IStartupRegistration, MacLoginItemRegistration>();
        services.AddSingleton<IWindowSystem, MacWindowSystem>();
        services.AddSingleton<IWindowOperations, MacWindowOperations>();
        services.AddSingleton<IProcessLauncher>(_ => new MacProcessLauncher());
        services.AddSingleton<IDisplayModes, MacDisplayModes>();
        services.AddSingleton<IClipboard, MacClipboard>();
        // open -a brings a running app forward by itself (Steps/OpenApp README), so the step always launches on a Mac.
        services.AddSingleton<IAppActivator>(NullAppActivator.Instance);
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
        UpgradeImportedSteps(session, log);
        if (!File.Exists(store.Location))
        {
            // First run: write the defaults and starter gestures now, so the file exists to look at and back up before any edit.
            session.Store.Save(session.Document);
            log.Info(ConfigLogSource, "Configuration created", ("file", store.Location));
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
        log.Info(ConfigLogSource, "Imported steps upgraded", ("steps", upgrade.Count), ("methods", methods));
    }

    private static EngineHost CreateHost(IServiceProvider sp)
    {
        var settings = sp.GetRequiredService<SettingsStore>();
        var gestures = sp.GetRequiredService<GestureLibrary>();
        var current = settings.Current;
        var initial = new EngineHostOptions(
            current.General.StrokeButton,
            current.Capture,
            EngineSettingsLink.ToModifiers(current.General.IgnoreKey),
            current.General.Enabled);
        return new EngineHost(BuildPorts(sp), () => gestures.All, () => settings.Current.Recognition, initial);
    }
}
