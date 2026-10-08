using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The Options view model round-trips through <see cref="SettingsStore"/> and follows every store change.</summary>
public sealed class AppSettingsViewModelTests
{
    [Fact]
    public void SettersWriteTheStore_OneVersionEach()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine);

        vm.StrokeButton = MouseButton.Middle;
        vm.StartDistancePx = 40;
        vm.TrailColour = new RgbColor(1, 2, 3);
        vm.Threshold = 80;

        var current = engine.Settings.Current;
        Assert.Equal(MouseButton.Middle, current.General.StrokeButton);
        Assert.Equal(40, current.Capture.StartDistancePx);
        Assert.Equal(new RgbColor(1, 2, 3), current.Trail.Colour);
        Assert.Equal(80, current.Recognition.Threshold);
        Assert.Equal(4, engine.Settings.Version);
    }

    [Fact]
    public void SettingTheSameValueIsNotAChange()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine);

        vm.StrokeButton = vm.StrokeButton;
        vm.TrailWidth = vm.TrailWidth;

        Assert.Equal(0, engine.Settings.Version);
    }

    [Fact]
    public void AStoreChangeFromOutsideRefreshesEveryBinding()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine);
        var raised = new List<string?>();
        vm.StrokeButton = MouseButton.X1;
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        engine.Settings.Undo();

        Assert.Equal(MouseButton.Right, vm.StrokeButton);
        Assert.Contains(string.Empty, raised);
    }

    [Fact]
    public void ARejectedValueLeavesTheStoreAlone_AndReportsWhy()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine);

        vm.TrailWidth = 500;

        Assert.Equal(5, vm.TrailWidth);
        Assert.Equal(0, engine.Settings.Version);
        Assert.NotNull(vm.LastError);
        Assert.Contains("Trail width", vm.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public void StartAtLoginGoesThroughAppState()
    {
        using var engine = new EngineFixture(start: false);
        var registration = new NullStartupRegistration();
        using var state = new AppState(engine.Settings, registration, NullEventLog.Instance, TestBuilds.Release);
        using var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action());
        using var vm = new AppSettingsViewModel(engine.Settings, state, detection);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.StartAtLogin = true;

        Assert.True(registration.IsEnabled);
        Assert.True(engine.Settings.Current.General.StartAtLogin);
        Assert.Contains(nameof(AppSettingsViewModel.StartAtLogin), raised);
    }

    [Fact]
    public void ConfigFolderIsTheAppPath()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine);

        Assert.Equal(AppPaths.ConfigFolder, vm.ConfigFolder);
    }

    private static AppSettingsViewModel Create(EngineFixture engine)
    {
        var state = new AppState(engine.Settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action());
        return new AppSettingsViewModel(engine.Settings, state, detection);
    }
}
