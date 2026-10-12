using System.Runtime.Versioning;
using Augram.App.Components.Capture;
using Augram.App.Hosting.Launch;
using Augram.App.Hosting.Startup;
using Augram.App.Overlay;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Training;
using Augram.Engine.Hosting;
using Augram.Engine.Input;
using Augram.Platform.MacOS;
using Augram.Platform.MacOS.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The engine's slice of the composition root: the input source and simulator, the overlay, the <see cref="EngineHost"/>,
/// the links that keep it in step with the settings (<see cref="EngineSettingsLink"/>, <see cref="EnginePauseState"/>,
/// <see cref="StrokeButtonDetection"/>), the engine's captures for hotkey fields and hold-remap buttons
/// (<see cref="EngineKeyCapture"/>, <see cref="EngineButtonCapture"/>) and the host's <see cref="IAppWindow"/>
/// (<see cref="MainWindowOpener"/>). <see cref="Register"/> only registers; <see cref="Start"/> runs on the UI thread once
/// Avalonia is up and starts the hook. Expects the diagnostics services (<see cref="IEventLog"/>, <see cref="HealthRegistry"/>,
/// <see cref="RecognitionLog"/>), the stores (<see cref="ConfigModule"/>) and the Platform adapters (<see cref="PlatformModule"/>).
/// The host's <see cref="EnginePorts.Intercept"/> is the training session when one is registered (<see cref="GesturesModule"/>);
/// it is resolved when the host is created, not when the module is registered, so the order of the modules does not matter.
/// <see cref="EngineHostOptions.SettleDelayMs"/> stays the default: there is no setting for it yet.
/// </summary>
public static class EngineModule
{
    public static IServiceCollection Register(IServiceCollection services, EngineModuleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= EngineModuleOptions.Default;
        var marshal = options.Marshal ?? UiThread.Post;

        services.AddSingleton<IClock>(SystemClock.Instance);
        // Shared by the hook and the simulator: the hook takes other programs' wheel events but not our own Scroll step's,
        // and other programs' button releases (plan 0005 decision 10) but not the ones we post.
        services.AddSingleton(_ => new OwnInjections());
        services.AddSingleton(sp => options.InputSource?.Invoke(sp) ?? new SharpHookInputSource(sp.GetRequiredService<IClock>(), sp.GetRequiredService<IEventLog>(), sp.GetRequiredService<OwnInjections>()));
        services.AddSingleton(sp => CreateSimulator(sp.GetRequiredService<OwnInjections>(), options.PlatformAdapters));
        // The Open app step's "This app (Augram)"; App attaches it once the main window exists.
        services.AddSingleton<MainWindowOpener>();
        services.AddSingleton<IAppWindow>(sp => sp.GetRequiredService<MainWindowOpener>());

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
        // Detect-to-assign for a hold remap command's buttons (plan 0002 step 4).
        services.AddSingleton<IButtonCapture>(sp => new EngineButtonCapture(sp.GetRequiredService<EngineHost>(), marshal));
        return services;
    }

    /// <summary>
    /// UI thread, after the framework initialised: creates the overlay (hidden until a stroke), links settings to the
    /// engine, installs the hook. Does nothing but log when <see cref="EngineKillSwitch"/> is set (<c>--no-engine</c>
    /// or <c>AUGRAM_NO_ENGINE=1</c>), so the UI can be run without touching the machine's input.
    /// </summary>
    /// <returns>True when the engine runs; false under the kill switch (<see cref="FormServices.Publish"/> then leaves the captures out).</returns>
    public static bool Start(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (EngineKillSwitch.IsSet())
        {
            services.GetRequiredService<IEventLog>().Info(LogSources.Engine, "Engine disabled by flag", ("argument", EngineKillSwitch.Argument), ("variable", EngineKillSwitch.EnvironmentVariable));
            return false;
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
        // The tray's projection and its start-at-login link follow the settings from here, as before they were two types.
        services.GetRequiredService<AppState>();
        services.GetRequiredService<StartAtLoginLink>().SyncStartupRegistration();
        host.Start();
        // The tray says "paused: VMware is focused" while a "disable while focused" app has focus (F5 ignore list).
        services.GetRequiredService<EnginePauseState>().Follow(host);
        return true;
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
    private static IInputSimulator CreateSimulator(OwnInjections own, bool adapters)
    {
        var simulator = new SharpHookInputSimulator(own);
        // Platform may not reference the Engine, so the macOS wrapper announces its releases through a delegate.
        return adapters && OperatingSystem.IsMacOS() ? new MacRemapButtonSimulator(new MacMediaKeySimulator(simulator), button => own.Expect(OwnInjections.SlotOf(button))) : simulator;
    }

    private static EngineHost CreateHost(IServiceProvider sp)
    {
        var settings = sp.GetRequiredService<SettingsStore>();
        var gestures = sp.GetRequiredService<GestureLibrary>();
        var current = settings.Current;
        var initial = new EngineHostOptions(
            current.General.StrokeButton,
            current.Capture,
            current.General.IgnoreKey,
            current.General.Enabled);
        return new EngineHost(BuildPorts(sp), () => gestures.All, () => settings.Current.Recognition, initial);
    }
}
