using System.Globalization;
using Augram.App.Components.GestureGrid;
using Augram.App.Import;
using Augram.App.Training;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The Gestures tab's projection over <see cref="GestureLibrary"/> (F3, F4, F5a, A7): tiles sorted by
/// name with their A7 confusion partners, undo/redo availability, the A7 "likely to be confused" line
/// (recomputed, debounced, after every library change; tiles are re-projected with it) and the handler that turns a <see cref="GestureGridActionEventArgs"/> into
/// a store call. Rules live in the library and <see cref="ConfusionCheck"/>; this only shows outcomes.
/// </summary>
public sealed partial class GesturesViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan DiagnosticDelay = TimeSpan.FromMilliseconds(200);

    private readonly GestureLibrary _library;
    private readonly Func<RecognitionOptions> _options;
    private readonly ITrainingPresenter _training;
    private readonly IImportPresenter _import;
    private readonly DispatcherTimer _diagnosticTimer;

    public GesturesViewModel(GestureLibrary library, Func<RecognitionOptions> options, ITrainingPresenter training, IImportPresenter import)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(import);
        _library = library;
        _options = options;
        _training = training;
        _import = import;
        _diagnosticTimer = new DispatcherTimer { Interval = DiagnosticDelay };
        _diagnosticTimer.Tick += (_, _) => RefreshDiagnostic();
        _library.Changed += OnLibraryChanged;
        Project();
        RefreshDiagnostic();
    }

    [ObservableProperty]
    public partial IReadOnlyList<GestureTileItem> Tiles { get; private set; } = [];

    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    [ObservableProperty]
    public partial bool CanRedo { get; private set; }

    /// <summary>Feedback for the last action: a rule message, or "Deleted X, undo with ...".</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ConfusionPair> ConfusionPairs { get; private set; } = [];

    /// <summary>The A7 line under the grid; null when no two active gestures confuse the recognizer.</summary>
    [ObservableProperty]
    public partial string? Diagnostic { get; private set; }

    public void Handle(GestureGridActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        try
        {
            Dispatch(e);
        }
        catch (GestureValidationException exception)
        {
            Message = exception.Message;
        }
    }

    /// <summary>Recomputes the confusion line now (the timer calls this after the debounce).</summary>
    public void RefreshDiagnostic()
    {
        _diagnosticTimer.Stop();
        ConfusionPairs = ConfusionCheck.Find(_library.All, _options());
        Project();
        Diagnostic = ConfusionPairs.Count == 0
            ? null
            : "Likely to be confused: " + string.Join(" · ", ConfusionPairs.Select(pair =>
                string.Create(CultureInfo.InvariantCulture, $"{pair.FirstName} and {pair.SecondName} ({Math.Round(pair.Score)}%)")));
    }

    public void Dispose()
    {
        _diagnosticTimer.Stop();
        _library.Changed -= OnLibraryChanged;
    }

    private void Dispatch(GestureGridActionEventArgs e)
    {
        switch (e.Action)
        {
            case GestureGridAction.New:
                _training.Open(TrainingRequest.NewGesture);
                break;
            case GestureGridAction.Redraw when e.Tile is { } tile:
                _training.Open(TrainingRequest.Redraw(tile.Id));
                break;
            case GestureGridAction.Rename when e.Tile is { } tile && e.Name is { } name:
                _library.Rename(tile.Id, name);
                break;
            case GestureGridAction.Delete when e.Tile is { } tile:
                var removed = _library.Remove(tile.Id);
                Message = $"Deleted '{removed.Name}'. {GestureGridKeymap.Undo} undoes it.";
                break;
            case GestureGridAction.KeepThis when e.Tile is { } tile:
                KeepThis(tile);
                break;
            case GestureGridAction.Import:
                _ = ImportAsync();
                break;
            case GestureGridAction.Undo:
                _library.Undo();
                break;
            case GestureGridAction.Redo:
                _library.Redo();
                break;
        }
    }

    /// <summary>Deletes the tile's exact duplicates (A7) as one undo step; the user chose which copy to keep.</summary>
    private void KeepThis(GestureTileItem tile)
    {
        var losers = tile.Partners.Where(partner => partner.Score >= ConfusionCheck.ExactCutOff).Select(partner => partner.Id).ToHashSet();
        if (losers.Count == 0)
        {
            Message = $"'{tile.Name}' has no exact duplicates.";
            return;
        }

        var names = _library.All.Where(gesture => losers.Contains(gesture.Id)).Select(gesture => "'" + gesture.Name + "'").ToList();
        _library.ReplaceAll(_library.All.Where(gesture => !losers.Contains(gesture.Id)));
        Message = $"Kept '{tile.Name}'; deleted {string.Join(", ", names)}. {GestureGridKeymap.Undo} undoes it.";
    }

    private async Task ImportAsync()
    {
        try
        {
            await _import.OpenAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Message = "Import failed: " + exception.Message;
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            OnLibraryChangedOnUi();
        }
        else
        {
            Dispatcher.UIThread.Post(OnLibraryChangedOnUi);
        }
    }

    private void OnLibraryChangedOnUi()
    {
        Project();
        _diagnosticTimer.Stop();
        _diagnosticTimer.Start();
    }

    private void Project()
    {
        Tiles = _library.All
            .OrderBy(gesture => gesture.Name, GestureRules.NameComparer)
            .Select(gesture => GestureTileItem.From(gesture, ConfusionPairs))
            .ToList();
        CanUndo = _library.CanUndo;
        CanRedo = _library.CanRedo;
    }
}
