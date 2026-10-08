using Augram.App.Hosting;
using Augram.App.Overlay;
using Augram.App.Tests.Support;
using Augram.App.Training;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using Augram.Platform.Windows.Display;
using Augram.Platform.Windows.WindowSystem;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The engine slice of the composition root resolves over a fake input source, starts, follows the settings store, and hands the host its M2 ports.</summary>
public sealed class EngineModuleTests
{
    [AvaloniaFact]
    public void RegistersEverything_StartsTheEngine_AndSavesTheConfig()
    {
        var folder = TempFolder();
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var services = Services(log);
        EngineModule.Register(services, Options(folder, source));

        try
        {
            using (var provider = services.BuildServiceProvider())
            {
                var session = provider.GetRequiredService<ConfigSession>();
                var settings = provider.GetRequiredService<SettingsStore>();
                var gestures = provider.GetRequiredService<GestureLibrary>();
                var host = provider.GetRequiredService<EngineHost>();
                Assert.Same(session.Settings, settings);
                Assert.Same(session.Gestures, gestures);
                Assert.Same(session.Mapping, provider.GetRequiredService<MappingStore>());
                Assert.NotEmpty(gestures.All);
                Assert.IsType<TrailOverlayWindow>(provider.GetRequiredService<IStrokeTrail>());
                Assert.IsType<NullStartupRegistration>(provider.GetRequiredService<IStartupRegistration>());
                Assert.Same(NullWindowSystem.Instance, provider.GetRequiredService<IWindowSystem>());
                Assert.Same(NullWindowOperations.Instance, provider.GetRequiredService<IWindowOperations>());
                Assert.NotNull(provider.GetRequiredService<StrokeButtonDetection>());
                Assert.True(log.Has(EngineModule.ConfigLogSource, "Configuration loaded"));

                EngineModule.Start(provider);

                Assert.Equal(1, source.StartCount);
                Assert.False(provider.GetRequiredService<TrailOverlayWindow>().IsVisible, "the overlay stays hidden until a stroke begins");
                Assert.True(log.Has(LogSources.Engine, "Engine starting"));

                // A settings change reaches the engine: the button rides the worker queue, Enabled is a volatile write.
                settings.SetGeneral(settings.Current.General with { StrokeButton = MouseButton.Middle, Enabled = false });
                settings.SetCapture(settings.Current.Capture with { StartDistancePx = 42 });
                EngineFixture.WaitFor(() => host.StrokeButton == MouseButton.Middle, "the stroke button change");
                EngineFixture.WaitFor(() => log.Has(LogSources.Engine, "Capture thresholds changed"), "the thresholds change");
                Assert.False(host.Enabled);
                Assert.True(log.Has(LogSources.Engine, "Engine disabled"));

                // The tray flag is the same setting.
                var state = provider.GetRequiredService<AppState>();
                Assert.False(state.Enabled);
                state.Toggle();
                Assert.True(host.Enabled);
            }

            Assert.Equal(1, source.StopCount);
            var saved = ConfigSerializer.Read(File.ReadAllText(Path.Combine(folder, FileConfigStore.FileName)));
            Assert.Equal(MouseButton.Middle, saved.Settings.General.StrokeButton);
            Assert.Equal(42, saved.Settings.Capture.StartDistancePx);
            Assert.NotEmpty(saved.Gestures);
        }
        finally
        {
            Delete(folder);
        }
    }

    [AvaloniaFact]
    public void PortsCarryTheLiveMappingAndRouteStrokesToTheTrainingSession()
    {
        var folder = TempFolder();
        var services = Services(new ListEventLog());
        var training = new FakeTrainingSession();
        services.AddSingleton<ITrainingSession>(training);
        EngineModule.Register(services, Options(folder, new FakeInputSource()));

        try
        {
            using var provider = services.BuildServiceProvider();
            var mapping = provider.GetRequiredService<MappingStore>();
            var ports = EngineModule.BuildPorts(provider);

            Assert.Same(NullWindowSystem.Instance, ports.Windows);
            Assert.Same(NullWindowOperations.Instance, ports.WindowOperations);
            Assert.Same(NullDisplayModes.Instance, ports.DisplayModes);
            Assert.Same(mapping.Current, ports.Mapping!());
            mapping.AddCommand(GroupId.Global, MappingFixture.Unbound("Minimize"));
            Assert.Same(mapping.Current, ports.Mapping());
            Assert.Single(ports.Mapping().Global.Commands);

            var stroke = new EngineEvent.NoMatch("no gesture", new CapturePoint(30, 40, 0), [new CapturePoint(30, 40, 0), new CapturePoint(60, 80, 5)], []);
            Assert.True(ports.Intercept!(stroke));
            var offered = Assert.Single(training.Offered);
            Assert.Equal((30, 40), (offered.StartX, offered.StartY));
            Assert.Equal(2, offered.Points.Count);
            training.Claims = false;
            Assert.False(ports.Intercept(stroke));
            Assert.False(ports.Intercept(new EngineEvent.WheelTriggered(WheelDirection.Up, new CapturePoint(0, 0, 0))), "wheel ticks are never training input");
        }
        finally
        {
            Delete(folder);
        }
    }

    [AvaloniaFact]
    public void InterceptIsAbsentWithoutATrainingSession()
    {
        var folder = TempFolder();
        var services = Services(new ListEventLog());
        EngineModule.Register(services, Options(folder, new FakeInputSource()));

        try
        {
            using var provider = services.BuildServiceProvider();
            Assert.Null(EngineModule.BuildPorts(provider).Intercept);
        }
        finally
        {
            Delete(folder);
        }
    }

    [Fact]
    public void WindowsAdaptersAreTheWin32OnesWhenPlatformAdaptersAreOn()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var folder = TempFolder();
        var services = Services(new ListEventLog());
        EngineModule.Register(services, Options(folder, new FakeInputSource()) with { PlatformAdapters = true });

        try
        {
            // Constructing the adapters installs nothing and touches no window; only the executor's calls would.
            using var provider = services.BuildServiceProvider();
            Assert.IsType<Win32WindowSystem>(provider.GetRequiredService<IWindowSystem>());
            Assert.IsType<Win32WindowOperations>(provider.GetRequiredService<IWindowOperations>());
            Assert.IsType<Win32DisplayModes>(provider.GetRequiredService<IDisplayModes>());
        }
        finally
        {
            Delete(folder);
        }
    }

    private static ServiceCollection Services(ListEventLog log)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventLog>(log);
        services.AddSingleton<HealthRegistry>();
        services.AddSingleton<RecognitionLog>();
        services.AddSingleton(TestBuilds.Release);
        services.AddSingleton<AppState>();
        return services;
    }

    private static EngineModuleOptions Options(string folder, FakeInputSource source) => new()
    {
        ConfigFolder = folder,
        InputSource = _ => source,
        PlatformAdapters = false,
        Marshal = action => action(),
    };

    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "augram-module-tests", Guid.NewGuid().ToString("N"));

    private static void Delete(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
