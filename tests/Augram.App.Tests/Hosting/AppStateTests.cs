using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The tray flags are a projection over the settings store, and start at login drives the OS registration.</summary>
public sealed class AppStateTests
{
    [Fact]
    public void ToggleFlipsThePersistedSetting_AndRaisesOnce()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        state.Toggle();
        Assert.False(state.Enabled);
        Assert.False(settings.Current.General.Enabled);

        state.Toggle();
        Assert.True(settings.Current.General.Enabled);
        Assert.Equal([nameof(AppState.Enabled), nameof(AppState.Enabled)], changes);
    }

    [Fact]
    public void SettingTheSameValueRecordsNothing()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance);
        var raised = 0;
        state.PropertyChanged += (_, _) => raised++;

        state.Enabled = true;
        state.StartAtLogin = false;

        Assert.Equal(0, raised);
        Assert.False(settings.CanUndo);
    }

    [Fact]
    public void StartAtLoginWritesTheRegistration_AndUndoRemovesIt()
    {
        var settings = new SettingsStore(Settings.Default);
        var registration = new NullStartupRegistration();
        using var state = new AppState(settings, registration, NullEventLog.Instance);

        state.StartAtLogin = true;
        Assert.True(registration.IsEnabled);
        Assert.True(settings.Current.General.StartAtLogin);

        settings.Undo();
        Assert.False(registration.IsEnabled);
        Assert.False(state.StartAtLogin);
    }

    [Fact]
    public void SyncAtStartupRepairsAMissingRegistration()
    {
        var settings = new SettingsStore(Settings.Default with { General = GeneralSettings.Default with { StartAtLogin = true } });
        var registration = new NullStartupRegistration();
        using var state = new AppState(settings, registration, NullEventLog.Instance);

        state.SyncStartupRegistration();

        Assert.True(registration.IsEnabled);
    }

    [Fact]
    public void ChangesFromTheStoreReachTheTray()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        settings.SetGeneral(settings.Current.General with { Enabled = false, StartAtLogin = true });

        Assert.Equal([nameof(AppState.Enabled), nameof(AppState.StartAtLogin)], changes);
    }
}
