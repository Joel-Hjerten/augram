using Augram.App.Hosting;
using Augram.Core.Steps.Hotkey;
using Avalonia;
using Velopack;

namespace Augram.App;

/// <summary>
/// Process entry point. Takes the single-instance guard (A10: a second launch asks the first to show
/// its window and exits), builds the service provider (the one composition root, ADR-0002 §2) and hands
/// it to the Avalonia <see cref="App"/>.
/// </summary>
internal static class Program
{
    public const string InstanceName = "Augram";
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run(); // First, always: answers the installer's hooks and exits for them (docs/release.md).
        using var guard = SingleInstanceGuard.TryAcquire(InstanceName);
        if (guard is null)
        {
            SingleInstanceGuard.SignalExisting(InstanceName, SignalTimeout);
            return;
        }

        // Hotkeys read with this platform's key names (Cmd and Opt on a Mac); stored values are the same everywhere.
        HotkeyText.Names = CommandsModule.CurrentPlatform;
        using var services = CompositionRoot.Build(guard);
        BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
