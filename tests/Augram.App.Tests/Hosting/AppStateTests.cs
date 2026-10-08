using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>
/// The tray flags are a projection over the settings store, and start at login drives the OS registration in the installed
/// build only: a development build never writes or removes it.
/// </summary>
public sealed class AppStateTests
{
    [Fact]
    public void ToggleFlipsThePersistedSetting_AndRaisesOnce()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
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
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
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
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Release);

        Assert.True(state.CanChangeStartAtLogin);
        state.StartAtLogin = true;
        Assert.True(registration.IsEnabled);
        Assert.True(settings.Current.General.StartAtLogin);

        settings.Undo();
        Assert.False(registration.IsEnabled);
        Assert.False(state.StartAtLogin);
    }

    [Fact]
    public void TheColourMenuBarIconIsSaved_AndRaisedOnUndo()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.False(state.ColourMenuBarIcon);
        state.ColourMenuBarIcon = true;
        Assert.True(settings.Current.General.ColourMenuBarIcon);

        settings.Undo();
        Assert.False(state.ColourMenuBarIcon);
        Assert.Equal([nameof(AppState.ColourMenuBarIcon), nameof(AppState.ColourMenuBarIcon)], changes);
    }

    [Fact]
    public void SyncAtStartupRepairsAMissingRegistration()
    {
        var settings = new SettingsStore(Settings.Default with { General = GeneralSettings.Default with { StartAtLogin = true } });
        var registration = new NullStartupRegistration();
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Release);

        state.SyncStartupRegistration();

        Assert.True(registration.IsEnabled);
    }

    [Fact]
    public void ChangesFromTheStoreReachTheTray()
    {
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        settings.SetGeneral(settings.Current.General with { Enabled = false, StartAtLogin = true });

        Assert.Equal([nameof(AppState.Enabled), nameof(AppState.StartAtLogin)], changes);
    }

    [Fact]
    public void DevBuild_NeverRemovesTheInstalledBuildsRegistration()
    {
        // The installed Augram registered itself; this config says off (or was never on); a dev run must leave the entry alone.
        var settings = new SettingsStore(Settings.Default);
        var registration = new RecordingStartupRegistration(enabled: true);
        var log = new ListEventLog();
        using var state = new AppState(settings, registration, log, TestBuilds.Dev);

        state.SyncStartupRegistration();

        Assert.True(registration.IsEnabled);
        Assert.Empty(registration.Writes);
        Assert.True(log.Has(AppState.LogSource, "Start at login left to the installed Augram"));
    }

    [Fact]
    public void DevBuild_NeverWritesTheRegistration_AtStartupOrFromTheStore()
    {
        var settings = new SettingsStore(Settings.Default with { General = GeneralSettings.Default with { StartAtLogin = true } });
        var registration = new RecordingStartupRegistration(enabled: false);
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Dev);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        state.SyncStartupRegistration();
        settings.SetGeneral(settings.Current.General with { StartAtLogin = false });
        settings.SetGeneral(settings.Current.General with { StartAtLogin = true });

        Assert.False(registration.IsEnabled);
        Assert.Empty(registration.Writes);
        Assert.Equal([nameof(AppState.StartAtLogin), nameof(AppState.StartAtLogin)], changes);
    }

    [Fact]
    public void DevBuild_CannotChangeTheSetting()
    {
        var settings = new SettingsStore(Settings.Default);
        var registration = new RecordingStartupRegistration();
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Dev);

        Assert.False(state.CanChangeStartAtLogin);
        state.StartAtLogin = true;

        Assert.False(state.StartAtLogin);
        Assert.False(settings.Current.General.StartAtLogin);
        Assert.False(settings.CanUndo);
        Assert.Empty(registration.Writes);
    }

    [Fact]
    public void ReleaseBuild_WritesAsBefore()
    {
        var settings = new SettingsStore(Settings.Default);
        var registration = new RecordingStartupRegistration(enabled: true);
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Release);

        state.SyncStartupRegistration();
        state.StartAtLogin = true;

        Assert.Equal([false, true], registration.Writes);
        Assert.True(registration.IsEnabled);
    }
}
