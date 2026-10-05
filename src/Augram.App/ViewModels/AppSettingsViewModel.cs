using System.ComponentModel;
using System.Runtime.CompilerServices;
using Augram.App.Hosting;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Recognition;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// What the Options page binds to. Holds a Core <see cref="Settings"/> value in memory for M1 step 6;
/// wiring it to <c>SettingsStore</c> (read <c>Current</c>, write through <c>Apply</c>) is the only change
/// this file needs later, because every property already round-trips the Core record.
/// <see cref="StartAtLogin"/> is shared with the tray through <see cref="AppState"/>.
/// </summary>
public sealed class AppSettingsViewModel : ObservableObject
{
    private readonly AppState _state;
    private Settings _settings = Settings.Default;

    public AppSettingsViewModel(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _state.PropertyChanged += OnStateChanged;
        ConfigFolder = AppPaths.ConfigFolder;
    }

    public Settings Current => _settings;

    public MouseButton StrokeButton
    {
        get => _settings.General.StrokeButton;
        set => Update(_settings with { General = _settings.General with { StrokeButton = value } });
    }

    public IgnoreKeys IgnoreKey
    {
        get => _settings.General.IgnoreKey;
        set => Update(_settings with { General = _settings.General with { IgnoreKey = value } });
    }

    public bool StartAtLogin
    {
        get => _state.StartAtLogin;
        set => _state.StartAtLogin = value;
    }

    public string ConfigFolder
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double StartDistancePx
    {
        get => _settings.Capture.StartDistancePx;
        set => Update(_settings with { Capture = _settings.Capture with { StartDistancePx = (int)Math.Round(value) } });
    }

    public double CancelDelayMs
    {
        get => _settings.Capture.CancelDelayMs;
        set => Update(_settings with { Capture = _settings.Capture with { CancelDelayMs = (int)Math.Round(value) } });
    }

    public NoMatchBehaviour NoMatch
    {
        get => _settings.NoMatch;
        set => Update(_settings with { NoMatch = value });
    }

    public RgbColor TrailColour
    {
        get => _settings.Trail.Colour;
        set => Update(_settings with { Trail = _settings.Trail with { Colour = value } });
    }

    public double TrailWidth
    {
        get => _settings.Trail.WidthPx;
        set => Update(_settings with { Trail = _settings.Trail with { WidthPx = value } });
    }

    public double TrailOpacity
    {
        get => _settings.Trail.Opacity;
        set => Update(_settings with { Trail = _settings.Trail with { Opacity = value } });
    }

    public double Threshold
    {
        get => _settings.Recognition.Threshold;
        set => Update(_settings with { Recognition = _settings.Recognition with { Threshold = value } });
    }

    public double Precision
    {
        get => _settings.Recognition.Precision;
        set => Update(_settings with { Recognition = _settings.Recognition with { Precision = (int)Math.Round(value) } });
    }

    public ScoringMode ScoringMode
    {
        get => _settings.Recognition.ScoringMode;
        set => Update(_settings with { Recognition = _settings.Recognition with { ScoringMode = value } });
    }

    private void Update(Settings next, [CallerMemberName] string? propertyName = null)
    {
        if (next == _settings)
        {
            return;
        }

        _settings = next;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Current));
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.StartAtLogin))
        {
            OnPropertyChanged(nameof(StartAtLogin));
        }
    }
}
