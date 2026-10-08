using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting;

/// <summary>
/// The two flags the tray and the Options page share, projected over the settings store (ADR-0002
/// §5a: no state of its own). <see cref="Enabled"/> is <c>GeneralSettings.Enabled</c>, persisted and
/// pushed to the engine by <see cref="EngineSettingsLink"/>. <see cref="StartAtLogin"/> is
/// <c>GeneralSettings.StartAtLogin</c>; whenever it changes (toggle, undo, load) the OS registration
/// (<see cref="IStartupRegistration"/>) is brought in step, and a registry failure is logged, never thrown.
/// Start at login belongs to the installed Augram (2026-10-08): a development build (<see cref="AppInfo.IsDev"/>) never
/// writes or removes the registration and cannot change the setting (<see cref="CanChangeStartAtLogin"/>), so logging in
/// always starts the installed Augram, never a stray dev copy, and a dev run never drops the installed one's entry.
/// </summary>
public sealed class AppState : ObservableObject, IDisposable
{
    public const string LogSource = "startup";

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

    public void Toggle() => Enabled = !Enabled;

    /// <summary>
    /// At startup: make the OS registration match the saved setting (it goes stale when the config is restored on another
    /// machine or the executable moves). A development build only says that it leaves the registration alone.
    /// </summary>
    public void SyncStartupRegistration()
    {
        if (!CanChangeStartAtLogin)
        {
            _log.Info(LogSource, "Start at login left to the installed Augram", ("channel", App.Channel));
            return;
        }

        ApplyStartup(StartAtLogin);
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
    }

    private void ApplyStartup(bool enabled)
    {
        if (!CanChangeStartAtLogin)
        {
            // The installed build's registration (or its absence) is never a development build's to change.
            return;
        }

        try
        {
            if (_startup.IsEnabled == enabled)
            {
                return;
            }

            _startup.Set(enabled);
            _log.Info(LogSource, enabled ? "Start at login registered" : "Start at login removed");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _log.Error(LogSource, "Could not change start at login", exception, ("enabled", enabled));
        }
    }
}
