using System.Security;
using Augram.App.Hosting.Launch;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting.Startup;

/// <summary>
/// Keeps the OS's start-at-login registration (<see cref="IStartupRegistration"/>) in step with
/// <c>GeneralSettings.StartAtLogin</c> (F7), as <see cref="EngineSettingsLink"/> keeps the engine in step with the settings:
/// whenever the setting changes (toggle, tray, undo, load) the registration follows as <see cref="StartupPolicy"/> says, and a
/// failure is logged, never thrown. At startup (<see cref="SyncStartupRegistration"/>) the setting follows the OS instead when
/// the user turned start at login off outside Augram, with a state note for Options (<see cref="Note"/>).
/// Start at login belongs to the installed Augram (2026-10-08): a development build (<see cref="AppInfo.IsDev"/>) never
/// reads, writes or removes the registration and cannot change the setting (<see cref="CanChange"/>), so logging in always
/// starts the installed Augram, never a stray dev copy, and a dev run never drops the installed one's entry.
/// <see cref="AppState"/> projects it for the tray and Options.
/// </summary>
public sealed class StartAtLoginLink : ObservableObject, IDisposable
{
    public const string FailedNote = "Could not change it; Diagnostics › Log says why.";

    private readonly SettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly IEventLog _log;
    private readonly SettingsFollower<bool> _setting;

    public StartAtLoginLink(SettingsStore settings, IStartupRegistration startup, IEventLog log, AppInfo app)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(app);
        _settings = settings;
        _startup = startup;
        _log = log;
        App = app;
        _setting = new SettingsFollower<bool>(settings, current => current.General.StartAtLogin, (_, now) => Apply(now));
    }

    /// <summary>This build: its channel decides whether start at login is this process's to manage.</summary>
    public AppInfo App { get; }

    /// <summary>False in a development build: the setting shows but cannot change, and nothing touches the OS.</summary>
    public bool CanChange => !App.IsDev;

    /// <summary>
    /// What Options says under the toggle, as plain text (a state note, App README "Help rule"): the setting followed an "off"
    /// made outside Augram ("Turned off in Task Manager › Startup apps."), macOS waits for approval, or the OS refused
    /// (<see cref="FailedNote"/>). Empty when there is nothing to say; for this run only.
    /// </summary>
    public string Note
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>
    /// At startup: make the OS registration match the saved setting (it goes stale when the config is restored on another
    /// machine, the executable moves, or an older build wrote it without <c>--hidden</c>), unless the user turned it off
    /// outside Augram: then the setting follows (<see cref="StartupPolicy.AtStartup"/>). A development build only says that
    /// it leaves the registration alone.
    /// </summary>
    public void SyncStartupRegistration()
    {
        if (!CanChange)
        {
            _log.Info(LogSources.Startup, "Start at login left to the installed Augram", ("channel", App.Channel));
            return;
        }

        if (ReadStatus() is not { } status)
        {
            return;
        }

        var wanted = _settings.Current.General.StartAtLogin;
        _log.Info(LogSources.Startup, "Start at login checked", ("setting", wanted), ("status", status));
        var action = StartupPolicy.AtStartup(wanted, status);
        if (action != StartupAction.FollowTheSystem)
        {
            Act(action, wanted, status);
            return;
        }

        _log.Warning(LogSources.Startup, "Start at login was turned off outside Augram; the setting follows", ("status", status));
        // The change brings the OS in step like any other (Apply: whatever is left is removed), then says why it is off.
        _settings.SetGeneral(_settings.Current.General with { StartAtLogin = false });
        Note = StartupPolicy.FollowedNote(status, RunningPlatform.Current);
    }

    public void Dispose() => _setting.Dispose();

    private void Apply(bool enabled)
    {
        if (!CanChange)
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
                _log.Info(LogSources.Startup, register ? "Start at login registered" : "Start at login removed", ("was", was), ("now", now));
            }

            Note = StartupPolicy.StatusNote(wanted, now);
        }
        catch (Exception exception) when (IsOsFailure(exception))
        {
            _log.Error(LogSources.Startup, "Could not change start at login", exception, ("enabled", wanted), ("was", was));
            Note = FailedNote;
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
            _log.Error(LogSources.Startup, "Could not read start at login", exception);
            Note = FailedNote;
            return null;
        }
    }

    private static bool IsOsFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or StartupRegistrationException;
}
