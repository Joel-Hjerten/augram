using Augram.App.Tests;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Augram.App.Tests;

/// <summary>Builds the headless Avalonia app that <c>[AvaloniaFact]</c> tests run inside, on the real composition root with logs sent to a temp folder.</summary>
public static class TestAppBuilder
{
    public static ServiceProvider Services { get; } =
        CompositionRoot.Build(
            logsFolder: Path.Combine(Path.GetTempPath(), "augram-app-tests", "logs"),
            configFolder: Path.Combine(Path.GetTempPath(), "augram-app-tests", "config"));

    public static AppBuilder BuildAvaloniaApp() =>
        Program.BuildAvaloniaApp(Services)
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
