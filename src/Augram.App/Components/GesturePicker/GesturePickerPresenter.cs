using Augram.App.Components.GestureGrid;
using Augram.App.Training;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// Shows the Select Gesture picker as a <see cref="GesturePickerWindow"/> owned by the main window
/// (F3). The tiles follow the library while the window is open. New Gesture… opens the shared
/// training popup through <see cref="ITrainingPresenter"/>; when that session ends Accepted, the
/// gesture the library gained is selected in the picker (the session reports only the outcome, so
/// the presenter diffs the library's ids around the training).
/// </summary>
public sealed class GesturePickerPresenter : IGesturePickerPresenter
{
    private readonly GestureLibrary _library;
    private readonly ITrainingPresenter _training;
    private readonly TrainingSession _session;

    public GesturePickerPresenter(GestureLibrary library, ITrainingPresenter training, TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(session);
        _library = library;
        _training = training;
        _session = session;
    }

    public async Task<GesturePickerResult> PickAsync(GestureId? current)
    {
        var picker = new GesturePicker { Tiles = Tiles() };
        picker.Select(current);
        var window = new GesturePickerWindow(picker);
        var answer = new TaskCompletionSource<GesturePickerResult>();
        void OnLibraryChanged(object? sender, EventArgs e) => picker.Tiles = Tiles();

        picker.Closed += (_, result) =>
        {
            answer.TrySetResult(result);
            window.Close();
        };
        picker.NewGestureRequested += (_, _) => BeginTraining(picker);
        _library.Changed += OnLibraryChanged;
        window.Closed += (_, _) =>
        {
            _library.Changed -= OnLibraryChanged;
            answer.TrySetResult(GesturePickerResult.Cancelled);
        };

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        window.Activate();
        return await answer.Task.ConfigureAwait(true);
    }

    private IReadOnlyList<GestureTileItem> Tiles() =>
        [.. _library.All.OrderBy(gesture => gesture.Name, GestureRules.NameComparer).Select(GestureTileItem.From)];

    private void BeginTraining(GesturePicker picker)
    {
        var before = _library.All.Select(gesture => gesture.Id).ToHashSet();
        _training.Open(TrainingRequest.NewGesture);
        void OnEnded(object? sender, TrainingOutcome outcome)
        {
            _session.Ended -= OnEnded;
            if (outcome == TrainingOutcome.Accepted && _library.All.FirstOrDefault(gesture => !before.Contains(gesture.Id)) is { } added)
            {
                picker.Select(added.Id);
            }
        }

        _session.Ended += OnEnded;
    }
}
