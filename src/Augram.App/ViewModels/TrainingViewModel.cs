using System.Globalization;
using Augram.App.Components.GestureDrawArea;
using Augram.App.Training;
using Augram.Core.Gestures;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.ViewModels;

/// <summary>
/// The training popup's projection of <see cref="TrainingSession"/> (F3): name, the stroke to show,
/// the live "Looks like X, N%" line, the rule message after a rejected Accept, and the two buttons.
/// Holds nothing the session does not; disposing it loses nothing.
/// </summary>
public sealed partial class TrainingViewModel : ObservableObject, IDisposable
{
    private readonly TrainingSession _session;

    public TrainingViewModel(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _session.Changed += OnSessionChanged;
        _session.Ended += OnSessionEnded;
    }

    /// <summary>The session ended; the window closes on this.</summary>
    public event EventHandler<TrainingOutcome>? Ended;

    public string Title => _session.Target is { } target ? "Add a sample to " + target.Name : "New gesture";

    public string Instruction => "Draw the gesture with the stroke button or the left mouse button. Drawing again replaces the stroke.";

    public string Name
    {
        get => _session.Name;
        set => _session.Name = value;
    }

    public IReadOnlyList<GesturePoint> Stroke => _session.Stroke;

    /// <summary>The cleaned shape drawn over the stroke while Clean up shape is ticked; empty otherwise.</summary>
    public IReadOnlyList<GesturePoint> CleanedStroke => _session.CleanUp ? _session.CleanedStroke : [];

    public bool CleanUp
    {
        get => _session.CleanUp;
        set => _session.CleanUp = value;
    }

    public bool HasStroke => _session.Stroke.Count > 0;

    public string BestMatchText => _session.BestMatch is { } best
        ? string.Create(CultureInfo.InvariantCulture, $"Looks like {best.Name}, {Math.Round(best.Score)}%")
        : HasStroke ? "No active gesture to compare with" : "Draw a stroke to see what it looks like";

    [ObservableProperty]
    public partial string? Message { get; private set; }

    public bool CanAccept => HasStroke && !string.IsNullOrWhiteSpace(Name);

    /// <summary>The draw area finished a left-button stroke.</summary>
    public void StrokeDrawn(IReadOnlyList<GesturePoint> points) => _session.ReplaceStroke(points);

    /// <summary>The draw area moved on screen (or left it: null); the session routes stroke-button strokes by this.</summary>
    public void CanvasMoved(ScreenArea? area) => _session.PublishCanvas(area);

    [RelayCommand]
    public void Accept()
    {
        try
        {
            _session.Accept();
        }
        catch (GestureValidationException exception)
        {
            Message = exception.Message;
        }
    }

    [RelayCommand]
    public void Cancel() => _session.Cancel();

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _session.Ended -= OnSessionEnded;
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        Message = null;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(CleanedStroke));
        OnPropertyChanged(nameof(CleanUp));
        OnPropertyChanged(nameof(HasStroke));
        OnPropertyChanged(nameof(BestMatchText));
        OnPropertyChanged(nameof(CanAccept));
    }

    private void OnSessionEnded(object? sender, TrainingOutcome outcome)
    {
        _session.PublishCanvas(null);
        Ended?.Invoke(this, outcome);
    }
}
