using Augram.App.Hosting.Startup;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>The tray flags are a projection over the settings store; start at login goes through the link and a development build cannot change it.</summary>
public sealed class AppStateTests
{
    [Fact]
    public void ToggleFlipsThePersistedSetting_AndRaisesOnce()
    {
        var settings = new SettingsStore(Settings.Default);
        using var link = new StartAtLoginLink(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        using var state = new AppState(settings, link);
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
        using var link = new StartAtLoginLink(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        using var state = new AppState(settings, link);
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
        using var link = new StartAtLoginLink(settings, registration, NullEventLog.Instance, TestBuilds.Release);
        using var state = new AppState(settings, link);

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
        using var link = new StartAtLoginLink(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        using var state = new AppState(settings, link);
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
    public void ChangesFromTheStoreReachTheTray()
    {
        var settings = new SettingsStore(Settings.Default);
        using var link = new StartAtLoginLink(settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Release);
        using var state = new AppState(settings, link);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        settings.SetGeneral(settings.Current.General with { Enabled = false, StartAtLogin = true });

        Assert.Equal([nameof(AppState.Enabled), nameof(AppState.StartAtLogin)], changes);
    }

    [Fact]
    public void DevBuild_CannotChangeTheSetting()
    {
        var settings = new SettingsStore(Settings.Default);
        var registration = new RecordingStartupRegistration();
        using var link = new StartAtLoginLink(settings, registration, NullEventLog.Instance, TestBuilds.Dev);
        using var state = new AppState(settings, link);

        Assert.False(state.CanChangeStartAtLogin);
        state.StartAtLogin = true;

        Assert.False(state.StartAtLogin);
        Assert.False(settings.Current.General.StartAtLogin);
        Assert.False(settings.CanUndo);
        Assert.Empty(registration.Writes);
    }

    private static SettingsStore SettingOn() =>
        new(Settings.Default with { General = GeneralSettings.Default with { StartAtLogin = true } });
}
