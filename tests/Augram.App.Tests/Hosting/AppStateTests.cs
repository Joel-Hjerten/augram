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

    [Fact]
    public void SyncLeavesAMatchingRegistrationAlone_AndLogsWhatItFound()
    {
        var registration = new RecordingStartupRegistration(StartupStatus.Registered);
        var log = new ListEventLog();
        using var state = new AppState(SettingOn(), registration, log, TestBuilds.Release);

        state.SyncStartupRegistration();

        Assert.Empty(registration.Writes);
        Assert.True(log.Has(AppState.LogSource, "Start at login checked"));
        Assert.Empty(state.StartAtLoginNote);
    }

    [Fact]
    public void SyncRewritesAnOutdatedEntry()
    {
        // The 0.5–0.7 builds registered the bare path, with no --hidden; a moved executable leaves another path.
        var registration = new RecordingStartupRegistration(StartupStatus.Outdated);
        var log = new ListEventLog();
        using var state = new AppState(SettingOn(), registration, log, TestBuilds.Release);

        state.SyncStartupRegistration();

        Assert.Equal([true], registration.Writes);
        Assert.Equal(StartupStatus.Registered, registration.Status);
        Assert.True(log.Has(AppState.LogSource, "Start at login registered"));
    }

    [Fact]
    public void SyncFollowsASwitchOffMadeOutsideAugram_AndSaysWhere()
    {
        var settings = SettingOn();
        var registration = new RecordingStartupRegistration(StartupStatus.DisabledByUser);
        var log = new ListEventLog();
        using var state = new AppState(settings, registration, log, TestBuilds.Release);
        var changes = new List<string?>();
        state.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        state.SyncStartupRegistration();

        Assert.False(state.StartAtLogin);
        Assert.False(settings.Current.General.StartAtLogin);
        // Never switched back on; the switched-off leftover is removed, so the OS and the setting agree.
        Assert.Equal([false], registration.Writes);
        Assert.Equal(StartupStatus.NotRegistered, registration.Status);
        Assert.Equal(StartupPolicy.FollowedNote(StartupStatus.DisabledByUser, AppState.Platform), state.StartAtLoginNote);
        Assert.NotEmpty(state.StartAtLoginNote);
        Assert.Contains(nameof(AppState.StartAtLogin), changes);
        Assert.Contains(nameof(AppState.StartAtLoginNote), changes);
        Assert.True(log.Has(AppState.LogSource, "Start at login was turned off outside Augram; the setting follows"));
    }

    [Fact]
    public void SyncFollowsMacOSWaitingForApproval()
    {
        var registration = new RecordingStartupRegistration(StartupStatus.NeedsApproval);
        using var state = new AppState(SettingOn(), registration, NullEventLog.Instance, TestBuilds.Release);

        state.SyncStartupRegistration();

        Assert.False(state.StartAtLogin);
        Assert.Equal([false], registration.Writes);
        Assert.Equal(StartupPolicy.FollowedNote(StartupStatus.NeedsApproval, AppState.Platform), state.StartAtLoginNote);
    }

    [Fact]
    public void TurningItOnAgainAfterFollowing_RegistersAndClearsTheNote()
    {
        var registration = new RecordingStartupRegistration(StartupStatus.DisabledByUser);
        using var state = new AppState(SettingOn(), registration, NullEventLog.Instance, TestBuilds.Release);
        state.SyncStartupRegistration();

        state.StartAtLogin = true;

        Assert.Equal([false, true], registration.Writes);
        Assert.Equal(StartupStatus.Registered, registration.Status);
        Assert.Empty(state.StartAtLoginNote);
    }

    [Fact]
    public void TurningItOnInAugram_ClearsASwitchOffMadeOutside()
    {
        // The setting is off (the user turned it off in Augram earlier) and Task Manager also says off: ticking it means on.
        var registration = new RecordingStartupRegistration(StartupStatus.DisabledByUser);
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Release);

        state.StartAtLogin = true;

        Assert.Equal([true], registration.Writes);
        Assert.Equal(StartupStatus.Registered, registration.Status);
    }

    [Fact]
    public void TurningItOnWhileMacOSWaitsForApproval_KeepsTheSetting_AndSaysSo()
    {
        var registration = new RecordingStartupRegistration(StartupStatus.NotRegistered) { AfterRegister = StartupStatus.NeedsApproval };
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Release);

        state.StartAtLogin = true;

        Assert.True(state.StartAtLogin);
        Assert.Equal("Waiting for approval in System Settings › General › Login Items.", state.StartAtLoginNote);
    }

    [Fact]
    public void AFailedWrite_IsLogged_AndNoted_NeverThrown()
    {
        var registration = new RecordingStartupRegistration { WriteFailure = new StartupRegistrationException("SMAppService register failed: Operation not permitted") };
        var log = new ListEventLog();
        var settings = new SettingsStore(Settings.Default);
        using var state = new AppState(settings, registration, log, TestBuilds.Release);

        state.StartAtLogin = true;

        Assert.True(state.StartAtLogin);
        Assert.Equal(AppState.FailedNote, state.StartAtLoginNote);
        Assert.True(log.Has(AppState.LogSource, "Could not change start at login"));

        registration.Status = StartupStatus.Registered;
        registration.WriteFailure = new IOException("registry");
        state.StartAtLogin = false;
        Assert.Equal([true, false], registration.Writes);
        Assert.Equal(AppState.FailedNote, state.StartAtLoginNote);
    }

    [Fact]
    public void AFailedRead_IsLogged_AndNothingIsWritten()
    {
        var registration = new RecordingStartupRegistration { ReadFailure = new UnauthorizedAccessException("denied") };
        var log = new ListEventLog();
        using var state = new AppState(SettingOn(), registration, log, TestBuilds.Release);

        state.SyncStartupRegistration();

        Assert.Empty(registration.Writes);
        Assert.True(state.StartAtLogin);
        Assert.True(log.Has(AppState.LogSource, "Could not read start at login"));
    }

    [Fact]
    public void DevBuild_NeverFollowsOrWrites_EvenWhenTurnedOffOutside()
    {
        var settings = SettingOn();
        var registration = new RecordingStartupRegistration(StartupStatus.DisabledByUser);
        using var state = new AppState(settings, registration, NullEventLog.Instance, TestBuilds.Dev);

        state.SyncStartupRegistration();

        Assert.True(settings.Current.General.StartAtLogin);
        Assert.Empty(registration.Writes);
        Assert.Empty(state.StartAtLoginNote);
    }

    private static SettingsStore SettingOn() =>
        new(Settings.Default with { General = GeneralSettings.Default with { StartAtLogin = true } });
}
