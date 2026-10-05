using Augram.App.Hosting;
using Augram.App.Overlay;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Engine.Hosting;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The engine slice of the composition root resolves over a fake input source, starts, and follows the settings store.</summary>
public sealed class EngineModuleTests
{
    [AvaloniaFact]
    public void RegistersEverything_StartsTheEngine_AndSavesTheConfig()
    {
        var folder = Path.Combine(Path.GetTempPath(), "augram-module-tests", Guid.NewGuid().ToString("N"));
        var source = new FakeInputSource();
        var log = new ListEventLog();
        var services = new ServiceCollection();
        services.AddSingleton<IEventLog>(log);
        services.AddSingleton<HealthRegistry>();
        services.AddSingleton<RecognitionLog>();
        services.AddSingleton<AppState>();
        EngineModule.Register(services, new EngineModuleOptions
        {
            ConfigFolder = folder,
            InputSource = _ => source,
            PlatformAdapters = false,
            Marshal = action => action(),
        });

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
                Assert.NotEmpty(gestures.All);
                Assert.IsType<TrailOverlayWindow>(provider.GetRequiredService<IStrokeTrail>());
                Assert.IsType<NullStartupRegistration>(provider.GetRequiredService<IStartupRegistration>());
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
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
