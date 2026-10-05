using Avalonia;

namespace Augram.App;

/// <summary>
/// Process entry point. Builds the service provider (the one composition root,
/// ADR-0002 §2) and hands it to the Avalonia <see cref="App"/>.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var services = CompositionRoot.Build();
        BuildAvaloniaApp(services).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .LogToTrace();
}
