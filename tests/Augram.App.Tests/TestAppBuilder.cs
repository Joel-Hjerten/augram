using Augram.App.Tests;
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Augram.App.Tests;

/// <summary>Builds the headless Avalonia app that <c>[AvaloniaFact]</c> tests run inside.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        Program.BuildAvaloniaApp(CompositionRoot.Build())
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
