using Augram.App.Hosting;
using Augram.Core.Steps.Hotkey;
using Avalonia;
using Velopack;

namespace Augram.App;

/// <summary>
/// Process entry point. Settles one-Augram-at-a-time first (A10, across the installed and development builds:
/// <see cref="InstanceStartup"/>): a second launch of the same install shows the running one and exits; a different build
/// is asked about once Avalonia runs, before anything else of it starts (<see cref="App"/>). Then it builds the service
/// provider (the one composition root, ADR-0002 §2) and hands it to the Avalonia <see cref="App"/>. Disposal runs in reverse:
/// the services (engine, config flush, sync) first, the single-instance name last, so a take-over waiting for the name
/// starts only after this process has let go of everything.
/// </summary>
internal static class Program
{
    public const string InstanceName = "Augram";

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run(); // First, always: answers the installer's hooks and exits for them (docs/release.md).
        var app = AppInfo.Current;
        using var startup = new InstanceStartup(InstanceName, InstanceIdentity.Current(app)) { IsHiddenLaunch = LaunchVisibility.HasHiddenArgument(args) };
        if (startup.Begin() == InstanceStartupStep.Exit)
        {
            return;
        }

        // Hotkeys read with this platform's key names (Cmd and Opt on a Mac); stored values are the same everywhere.
        HotkeyText.Names = CommandsModule.CurrentPlatform;
        using var services = CompositionRoot.Build(app);
        BuildAvaloniaApp(services, startup).StartWithClassicDesktopLifetime(args);
    }

    /// <param name="services">The composition root's provider.</param>
    /// <param name="startup">The launch's single-instance state; null in the headless tests.</param>
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services, InstanceStartup? startup = null) =>
        AppBuilder.Configure(() => new App(services, startup))
            .UsePlatformDetect()
            .LogToTrace();
}
