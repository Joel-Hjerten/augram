using System.ComponentModel;
using Augram.App.Hosting;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Recognition;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// What the Options page binds to: a projection over <see cref="SettingsStore"/> (ADR-0002 §5a). Every
/// getter reads <c>Current</c>; every setter is one undo step through the store; every store change,
/// undo and redo included, refreshes all bindings at once (an empty property name). Start at login
/// goes through <see cref="AppState"/>, which also keeps the OS registration in step, and detect-to-assign
/// through <see cref="StrokeButtonDetection"/>. A rejected value (out of range) leaves the store untouched
/// and is reported in <see cref="LastError"/>.
/// </summary>
public sealed class AppSettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _settings;
    private readonly AppState _state;
    private readonly StrokeButtonDetection _detection;

    public AppSettingsViewModel(SettingsStore settings, AppState state, StrokeButtonDetection detection)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(detection);
        _settings = settings;
        _state = state;
        _detection = detection;
        ConfigFolder = AppPaths.ConfigFolder;
        _settings.Changed += OnSettingsChanged;
        _state.PropertyChanged += OnStateChanged;
        _detection.PropertyChanged += OnDetectionChanged;
    }

    public Settings Current => _settings.Current;

    public MouseButton StrokeButton
    {
        get => Current.General.StrokeButton;
        set => Apply(s => s with { General = s.General with { StrokeButton = value } });
    }

    public IgnoreKeys IgnoreKey
    {
        get => Current.General.IgnoreKey;
        set => Apply(s => s with { General = s.General with { IgnoreKey = value } });
    }

    public bool StartAtLogin
    {
        get => _state.StartAtLogin;
        set => _state.StartAtLogin = value;
    }

    /// <summary>Read-only for now: change it with <c>--config-folder &lt;path&gt;</c> (A17).</summary>
    public string ConfigFolder { get; }

    public double StartDistancePx
    {
        get => Current.Capture.StartDistancePx;
        set => Apply(s => s with { Capture = s.Capture with { StartDistancePx = (int)Math.Round(value) } });
    }

    public double CancelDelayMs
    {
        get => Current.Capture.CancelDelayMs;
        set => Apply(s => s with { Capture = s.Capture with { CancelDelayMs = (int)Math.Round(value) } });
    }

    public NoMatchBehaviour NoMatch
    {
        get => Current.NoMatch;
        set => Apply(s => s with { NoMatch = value });
    }

    public RgbColor TrailColour
    {
        get => Current.Trail.Colour;
        set => Apply(s => s with { Trail = s.Trail with { Colour = value } });
    }

    public double TrailWidth
    {
        get => Current.Trail.WidthPx;
        set => Apply(s => s with { Trail = s.Trail with { WidthPx = value } });
    }

    public double TrailOpacity
    {
        get => Current.Trail.Opacity;
        set => Apply(s => s with { Trail = s.Trail with { Opacity = value } });
    }

    public double Threshold
    {
        get => Current.Recognition.Threshold;
        set => Apply(s => s with { Recognition = s.Recognition with { Threshold = value } });
    }

    public double Precision
    {
        get => Current.Recognition.Precision;
        set => Apply(s => s with { Recognition = s.Recognition with { Precision = (int)Math.Round(value) } });
    }

    public ScoringMode ScoringMode
    {
        get => Current.Recognition.ScoringMode;
        set => Apply(s => s with { Recognition = s.Recognition with { ScoringMode = value } });
    }

    public bool CanUndo => _settings.CanUndo;

    public bool CanRedo => _settings.CanRedo;

    public string DetectStatus => _detection.Status;

    public bool IsDetecting => _detection.IsListening;

    /// <summary>The last validation message from the store, or null; cleared by the next accepted change.</summary>
    public string? LastError { get; private set => SetProperty(ref field, value); }

    public void Undo() => _settings.Undo();

    public void Redo() => _settings.Redo();

    public void DetectButton() => _detection.Start();

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _state.PropertyChanged -= OnStateChanged;
        _detection.PropertyChanged -= OnDetectionChanged;
    }

    private void Apply(Func<Settings, Settings> change)
    {
        var next = change(Current);
        if (next == Current)
        {
            return;
        }

        try
        {
            _settings.Apply(_ => next);
            LastError = null;
        }
        catch (SettingsValidationException exception)
        {
            LastError = exception.Message;
            OnPropertyChanged(string.Empty);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => OnPropertyChanged(string.Empty);

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.StartAtLogin))
        {
            OnPropertyChanged(nameof(StartAtLogin));
        }
    }

    private void OnDetectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(DetectStatus));
        OnPropertyChanged(nameof(IsDetecting));
    }
}
