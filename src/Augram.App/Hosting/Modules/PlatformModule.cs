using System.Runtime.Versioning;
using Augram.App.Hosting.Launch;
using Augram.Core.Abstractions;
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
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The Platform adapters' slice of the composition root: the Windows or macOS adapter for each Core port (cursor probe, system
/// events, overlay style or the macOS trail panel, start at login, window system and operations, process launcher, display
/// modes, clipboard, app activator, keyboard layouts), or the null objects when <c>adapters</c> is false (tests) or the OS is neither.
/// Constructing an adapter installs nothing and touches no window; the engine's calls do (<see cref="EngineModule"/>). The
/// input source and simulator are SharpHook's and live with the engine.
/// </summary>
public static class PlatformModule
{
    /// <param name="services">The collection.</param>
    /// <param name="adapters">False registers the null adapters (no cursor probe, no system events, no Win32 styles, in-memory start-at-login) instead of the Platform ones.</param>
    public static IServiceCollection Register(IServiceCollection services, bool adapters = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (adapters && OperatingSystem.IsWindows())
        {
            RegisterWindows(services);
            return services;
        }

        if (adapters && OperatingSystem.IsMacOS())
        {
            RegisterMacOS(services);
            return services;
        }

        services.AddSingleton<IOverlayWindowStyle>(NullOverlayWindowStyle.Instance);
        services.AddSingleton<IStartupRegistration, NullStartupRegistration>();
        services.AddSingleton<IWindowSystem>(NullWindowSystem.Instance);
        services.AddSingleton<IWindowOperations>(NullWindowOperations.Instance);
        services.AddSingleton<IProcessLauncher>(NullProcessLauncher.Instance);
        services.AddSingleton<IDisplayModes>(NullDisplayModes.Instance);
        services.AddSingleton<IClipboard>(NullClipboard.Instance);
        services.AddSingleton<IKeyboardLayouts>(NullKeyboardLayouts.Instance);
        services.AddSingleton<IAppActivator>(NullAppActivator.Instance);
        return services;
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
        // Win32KeyboardLayouts is plan 0011 step 5 (when Joel is on the PC); until then the PC knows no layouts and reads US key names.
        services.AddSingleton<IKeyboardLayouts>(NullKeyboardLayouts.Instance);
        services.AddSingleton<IAppActivator>(sp => new Win32AppActivator(sp.GetRequiredService<IEventLog>()));
    }

    /// <summary>
    /// The macOS adapters: Accessibility-API window system and operations (close, minimize, maximize/restore so far), a
    /// verified click-through overlay (the trail's own panel, <see cref="MacTrailPanel"/>), the cursor probe, and the system
    /// events (<see cref="MacSystemEvents"/>: a new frontmost app for hold remaps, sleep and wake, lock and unlock; resolved
    /// with the host in <see cref="EngineModule.Start"/>, on the main thread, where its observers must be registered), start
    /// at login as a login item (<see cref="MacLoginItemRegistration"/>, SMAppService), and the keyboard layouts
    /// (<see cref="MacKeyboardLayouts"/>, Text Input Source Services on the main thread, plan 0011).
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
        services.AddSingleton<IKeyboardLayouts, MacKeyboardLayouts>();
        // open -a brings a running app forward by itself (Steps/OpenApp README), so the step always launches on a Mac.
        services.AddSingleton<IAppActivator>(NullAppActivator.Instance);
    }
}
