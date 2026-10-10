using System.Security;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting;

/// <summary>
/// The flags the tray and the Options page share (and macOS's <see cref="ColourMenuBarIcon"/>), projected over the settings store (ADR-0002
/// §5a: no state of its own, beyond this run's <see cref="StartAtLoginNote"/>). <see cref="Enabled"/> is <c>GeneralSettings.Enabled</c>, persisted and
/// pushed to the engine by <see cref="EngineSettingsLink"/>. <see cref="StartAtLogin"/> is
/// <c>GeneralSettings.StartAtLogin</c>; whenever it changes (toggle, undo, load) the OS registration
/// (<see cref="IStartupRegistration"/>) is brought in step as <see cref="StartupPolicy"/> says, and a failure is logged, never thrown.
/// At startup the setting follows the OS when the user turned start at login off outside Augram, with a state note for Options
/// (<see cref="StartAtLoginNote"/>).
/// Start at login belongs to the installed Augram (2026-10-08): a development build (<see cref="AppInfo.IsDev"/>) never
/// reads, writes or removes the registration and cannot change the setting (<see cref="CanChangeStartAtLogin"/>), so logging in
/// always starts the installed Augram, never a stray dev copy, and a dev run never drops the installed one's entry.
/// </summary>
public sealed class AppState : ObservableObject, IDisposable
{
    public const string LogSource = "startup";
    public const string FailedNote = "Could not change it; Diagnostics › Log says why.";

    private readonly SettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly IEventLog _log;
    private GeneralSettings _last;

    public AppState(SettingsStore settings, IStartupRegistration startup, IEventLog log, AppInfo app)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(app);
        _settings = settings;
        _startup = startup;
        _log = log;
        App = app;
        _last = settings.Current.General;
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>The platform whose wording the state notes use (Task Manager or System Settings).</summary>
    public static HostPlatform Platform => OperatingSystem.IsMacOS() ? HostPlatform.MacOS : HostPlatform.Windows;

    /// <summary>This build: its channel decides whether start at login is this process's to manage.</summary>
    public AppInfo App { get; }

    public bool Enabled
    {
        get => _settings.Current.General.Enabled;
        set => Change(general => general with { Enabled = value });
    }

    /// <summary>False in a development build: the toggle shows the setting but is disabled, and nothing touches the OS.</summary>
    public bool CanChangeStartAtLogin => !App.IsDev;

    /// <summary>The saved choice. Setting it does nothing in a development build (<see cref="CanChangeStartAtLogin"/>).</summary>
    public bool StartAtLogin
    {
        get => _settings.Current.General.StartAtLogin;
        set
        {
            if (CanChangeStartAtLogin)
            {
                Change(general => general with { StartAtLogin = value });
            }
        }
    }

    /// <summary>
    /// What Options says under the toggle, as plain text (a state note, App README "Help rule"): the setting followed an "off"
    /// made outside Augram ("Turned off in Task Manager › Startup apps."), macOS waits for approval, or the OS refused
    /// (<see cref="FailedNote"/>). Empty when there is nothing to say; for this run only.
    /// </summary>
    public string StartAtLoginNote
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>macOS: the colour app icon in the menu bar instead of the tinted template (Options › General, as in Eyeris).</summary>
    public bool ColourMenuBarIcon
    {
        get => _settings.Current.General.ColourMenuBarIcon;
        set => Change(general => general with { ColourMenuBarIcon = value });
    }

    /// <summary>Only the macOS menu bar has a template icon to choose against; Windows trays are always in colour.</summary>
    public static bool CanChooseMenuBarIcon => OperatingSystem.IsMacOS();

    public void Toggle() => Enabled = !Enabled;

    /// <summary>
    /// At startup: make the OS registration match the saved setting (it goes stale when the config is restored on another
    /// machine, the executable moves, or an older build wrote it without <c>--hidden</c>), unless the user turned it off
    /// outside Augram: then the setting follows (<see cref="StartupPolicy.AtStartup"/>). A development build only says that
    /// it leaves the registration alone.
    /// </summary>
    public void SyncStartupRegistration()
    {
        if (!CanChangeStartAtLogin)
        {
            _log.Info(LogSource, "Start at login left to the installed Augram", ("channel", App.Channel));
            return;
        }

        if (ReadStatus() is not { } status)
        {
            return;
        }

        var wanted = StartAtLogin;
        _log.Info(LogSource, "Start at login checked", ("setting", wanted), ("status", status));
        var action = StartupPolicy.AtStartup(wanted, status);
        if (action != StartupAction.FollowTheSystem)
        {
            Act(action, wanted, status);
            return;
        }

        _log.Warning(LogSource, "Start at login was turned off outside Augram; the setting follows", ("status", status));
        // The change brings the OS in step like any other (OnChange: whatever is left is removed), then says why it is off.
        Change(general => general with { StartAtLogin = false });
        StartAtLoginNote = StartupPolicy.FollowedNote(status, Platform);
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    private void Change(Func<GeneralSettings, GeneralSettings> change)
    {
        var current = _settings.Current.General;
        var next = change(current);
        if (next != current)
        {
            _settings.SetGeneral(next);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var now = _settings.Current.General;
        var last = _last;
        _last = now;
        if (now.Enabled != last.Enabled)
        {
            OnPropertyChanged(nameof(Enabled));
        }

        if (now.StartAtLogin != last.StartAtLogin)
        {
            ApplyStartup(now.StartAtLogin);
            OnPropertyChanged(nameof(StartAtLogin));
        }

        if (now.ColourMenuBarIcon != last.ColourMenuBarIcon)
        {
            OnPropertyChanged(nameof(ColourMenuBarIcon));
        }
    }

    private void ApplyStartup(bool enabled)
    {
        if (!CanChangeStartAtLogin)
        {
            // The installed build's registration (or its absence) is never a development build's to change.
            return;
        }

        if (ReadStatus() is { } status)
        {
            Act(StartupPolicy.OnChange(enabled, status), enabled, status);
        }
    }

    private void Act(StartupAction action, bool wanted, StartupStatus was)
    {
        try
        {
            var now = was;
            if (action is StartupAction.Register or StartupAction.Unregister)
            {
                var register = action == StartupAction.Register;
                _startup.Set(register);
                now = _startup.Status;
                _log.Info(LogSource, register ? "Start at login registered" : "Start at login removed", ("was", was), ("now", now));
            }

            StartAtLoginNote = StartupPolicy.StatusNote(wanted, now);
        }
        catch (Exception exception) when (IsOsFailure(exception))
        {
            _log.Error(LogSource, "Could not change start at login", exception, ("enabled", wanted), ("was", was));
            StartAtLoginNote = FailedNote;
        }
    }

    private StartupStatus? ReadStatus()
    {
        try
        {
            return _startup.Status;
        }
        catch (Exception exception) when (IsOsFailure(exception))
        {
            _log.Error(LogSource, "Could not read start at login", exception);
            StartAtLoginNote = FailedNote;
            return null;
        }
    }

    private static bool IsOsFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or StartupRegistrationException;
}
