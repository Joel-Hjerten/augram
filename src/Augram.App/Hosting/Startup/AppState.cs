using System.ComponentModel;
using Augram.App.Hosting.Launch;
using Augram.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting.Startup;

/// <summary>
/// The flags the tray and the Options page share (and macOS's <see cref="ColourMenuBarIcon"/>), projected over the settings
/// store (ADR-0002 §5a: no state of its own). <see cref="Enabled"/> is <c>GeneralSettings.Enabled</c>, persisted and pushed to
/// the engine by <see cref="EngineSettingsLink"/>. <see cref="StartAtLogin"/> is <c>GeneralSettings.StartAtLogin</c>; the OS
/// registration follows it through <see cref="StartAtLoginLink"/>, whose note and development-build rule this projects
/// (<see cref="StartAtLoginNote"/>, <see cref="CanChangeStartAtLogin"/>).
/// </summary>
public sealed class AppState : ObservableObject, IDisposable
{
    private readonly SettingsStore _settings;
    private readonly StartAtLoginLink _startAtLogin;
    private readonly SettingsFollower<GeneralSettings> _general;

    /// <param name="settings">The settings store.</param>
    /// <param name="startAtLogin">The link to the OS registration; made first, so on a change the registration is in step before the tray hears of it.</param>
    public AppState(SettingsStore settings, StartAtLoginLink startAtLogin)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(startAtLogin);
        _settings = settings;
        _startAtLogin = startAtLogin;
        _startAtLogin.PropertyChanged += OnStartAtLoginChanged;
        _general = new SettingsFollower<GeneralSettings>(settings, current => current.General, OnGeneralChanged);
    }

    /// <summary>This build: its channel decides whether start at login is this process's to manage.</summary>
    public AppInfo App => _startAtLogin.App;

    public bool Enabled
    {
        get => _settings.Current.General.Enabled;
        set => Change(general => general with { Enabled = value });
    }

    /// <summary>False in a development build: the toggle shows the setting but is disabled, and nothing touches the OS.</summary>
    public bool CanChangeStartAtLogin => _startAtLogin.CanChange;

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

    /// <summary>What Options says under the toggle (<see cref="StartAtLoginLink.Note"/>); empty when there is nothing to say.</summary>
    public string StartAtLoginNote => _startAtLogin.Note;

    /// <summary>macOS: the colour app icon in the menu bar instead of the tinted template (Options › General, as in Eyeris).</summary>
    public bool ColourMenuBarIcon
    {
        get => _settings.Current.General.ColourMenuBarIcon;
        set => Change(general => general with { ColourMenuBarIcon = value });
    }

    /// <summary>Only the macOS menu bar has a template icon to choose against; Windows trays are always in colour.</summary>
    public static bool CanChooseMenuBarIcon => OperatingSystem.IsMacOS();

    public void Toggle() => Enabled = !Enabled;

    public void Dispose()
    {
        _general.Dispose();
        _startAtLogin.PropertyChanged -= OnStartAtLoginChanged;
    }

    private void Change(Func<GeneralSettings, GeneralSettings> change)
    {
        var current = _settings.Current.General;
        var next = change(current);
        if (next != current)
        {
            _settings.SetGeneral(next);
        }
    }

    private void OnStartAtLoginChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StartAtLoginLink.Note))
        {
            OnPropertyChanged(nameof(StartAtLoginNote));
        }
    }

    private void OnGeneralChanged(GeneralSettings last, GeneralSettings now)
    {
        if (now.Enabled != last.Enabled)
        {
            OnPropertyChanged(nameof(Enabled));
        }

        if (now.StartAtLogin != last.StartAtLogin)
        {
            OnPropertyChanged(nameof(StartAtLogin));
        }

        if (now.ColourMenuBarIcon != last.ColourMenuBarIcon)
        {
            OnPropertyChanged(nameof(ColourMenuBarIcon));
        }
    }
}
