using Augram.App.Components.GestureGrid;
using Augram.App.Components.SearchBox;
using Augram.App.Dialogs;
using Augram.App.Training;
using Augram.Core.Gestures;
using Augram.Core.Training;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// Shows the Select Gesture picker as a <see cref="GesturePickerWindow"/> owned by the main window
/// (F3). The tiles follow the library while the window is open, narrowed by the picker's search box (plan 0009: the gestures
/// whose name starts with the text first, then the rest, each half by name; <see cref="Tiles"/>). New Gesture… opens the shared
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
        var picker = new GesturePicker { Tiles = Tiles(_library.All, SearchQuery.Empty) };
        picker.Select(current);
        var window = new GesturePickerWindow(picker);
        var answer = new TaskCompletionSource<GesturePickerResult>();
        void Refilter() => picker.Tiles = Tiles(_library.All, SearchQuery.Parse(picker.SearchText));
        void OnLibraryChanged(object? sender, EventArgs e) => Refilter();
        picker.PropertyChanged += (_, e) =>
        {
            if (e.Property == GesturePicker.SearchTextProperty)
            {
                Refilter();
            }
        };

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

        DialogHost.Show(window);
        return await answer.Task.ConfigureAwait(true);
    }

    /// <summary>The picker's tiles for a search (plan 0009 decision 2): the matches whose name starts with the text first, then the other matches, each by name.</summary>
    internal static IReadOnlyList<GestureTileItem> Tiles(IEnumerable<Gesture> gestures, SearchQuery search)
    {
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(search);
        return
        [
            .. gestures
                .Where(gesture => search.Matches(gesture.Name))
                .OrderByDescending(gesture => search.StartsWith(gesture.Name))
                .ThenBy(gesture => gesture.Name, GestureRules.NameComparer)
                .Select(GestureTileItem.From),
        ];
    }

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
