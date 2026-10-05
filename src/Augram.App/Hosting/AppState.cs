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
/// </summary>
public sealed class AppState : ObservableObject, IDisposable
{
    public const string LogSource = "startup";

    private readonly SettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly IEventLog _log;
    private GeneralSettings _last;

    public AppState(SettingsStore settings, IStartupRegistration startup, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(log);
        _settings = settings;
        _startup = startup;
        _log = log;
        _last = settings.Current.General;
        settings.Changed += OnSettingsChanged;
    }

    public bool Enabled
    {
        get => _settings.Current.General.Enabled;
        set => Change(general => general with { Enabled = value });
    }

    public bool StartAtLogin
    {
        get => _settings.Current.General.StartAtLogin;
        set => Change(general => general with { StartAtLogin = value });
    }

    public void Toggle() => Enabled = !Enabled;

    /// <summary>At startup: make the OS registration match the saved setting (it goes stale when the config is restored on another machine or the executable moves).</summary>
    public void SyncStartupRegistration() => ApplyStartup(StartAtLogin);

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
