using Augram.App.Components.GestureGrid;
using Augram.App.Import;
using Augram.App.Training;
using Augram.App.UsedBy;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The Gestures tab's projection over <see cref="GestureLibrary"/> (F3, F4, F5a, A7): tiles sorted by
/// name with their A7 duplicates (pairs at or above the duplicate cut-off, recomputed, debounced, after
/// every library change; the tiles outline them and light them up on select), undo/redo availability and the handler that turns a <see cref="GestureGridActionEventArgs"/> into
/// a store call. Rules live in the library and <see cref="ConfusionCheck"/>; this only shows outcomes.
/// The <see cref="MappingStore"/> is read for "Used by…" and written when a delete or "Keep this" changes
/// which gesture a command is bound to; those edits are the commands' own undo steps on the Commands
/// tab, apart from the library's (two stores, two histories: accepted for M2).
/// </summary>
public sealed partial class GesturesViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan DiagnosticDelay = TimeSpan.FromMilliseconds(200);

    private readonly GestureLibrary _library;
    private readonly MappingStore _mapping;
    private readonly GestureBindings _bindings;
    private readonly Func<RecognitionOptions> _options;
    private readonly ITrainingPresenter _training;
    private readonly IImportPresenter _import;
    private readonly IUsedByPresenter _usedBy;
    private readonly IConfirmPresenter _confirm;
    private readonly DispatcherTimer _diagnosticTimer;

    public GesturesViewModel(
        GestureLibrary library,
        Func<RecognitionOptions> options,
        ITrainingPresenter training,
        IImportPresenter import,
        MappingStore mapping,
        IUsedByPresenter usedBy,
        IConfirmPresenter confirm)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(usedBy);
        ArgumentNullException.ThrowIfNull(confirm);
        _library = library;
        _mapping = mapping;
        _bindings = new GestureBindings(mapping);
        _options = options;
        _training = training;
        _import = import;
        _usedBy = usedBy;
        _confirm = confirm;
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

    public void Handle(GestureGridActionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        Message = null;
        try
        {
            Dispatch(e);
        }
        catch (Exception exception) when (exception is GestureValidationException or MappingValidationException)
        {
            Message = exception.Message;
        }
    }

    /// <summary>Recomputes the confusion pairs now and re-projects the tiles with them (the timer calls this after the debounce).</summary>
    public void RefreshDiagnostic()
    {
        _diagnosticTimer.Stop();
        ConfusionPairs = ConfusionCheck.Find(_library.All, _options(), ConfusionCheck.DuplicateCutOff);
        Project();
    }

    public void Dispose()
    {
        _diagnosticTimer.Stop();
        _library.Changed -= OnLibraryChanged;
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

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
            case GestureGridAction.UsedBy when e.Tile is { } tile:
                _usedBy.Show(tile.Id);
                break;
            case GestureGridAction.Delete when e.Tile is { } tile:
                _ = DeleteAsync(tile);
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

    /// <summary>
    /// An unused gesture goes at once (A9: undo brings it back). A used one asks first, naming its commands;
    /// on yes the commands stay but lose their trigger (<see cref="Trigger.None"/>), then the gesture goes.
    /// </summary>
    private async Task DeleteAsync(GestureTileItem tile)
    {
        var used = UsedByRow.For(_mapping, tile.Id);
        if (used.Count > 0)
        {
            var question = $"Delete '{tile.Name}'? It is used by {string.Join(", ", used.Select(row => row.Label))}. Those commands lose their gesture.";
            if (!await _confirm.ConfirmAsync("Delete gesture", question, "Delete").ConfigureAwait(true))
            {
                return;
            }
        }

        try
        {
            var unbound = _bindings.Unbind(tile.Id);
            var removed = _library.Remove(tile.Id);
            Message = unbound == 0
                ? $"Deleted '{removed.Name}'. {GestureGridKeymap.Undo} undoes it."
                : $"Deleted '{removed.Name}' and unbound {Count(unbound, "command")}. {GestureGridKeymap.Undo} undoes the delete; the Commands tab undoes the unbinding.";
        }
        catch (Exception exception) when (exception is GestureValidationException or MappingValidationException)
        {
            Message = exception.Message;
        }
    }

    /// <summary>
    /// Deletes the tile's duplicates (A7) as one library undo step; the user chose which copy to keep. Every
    /// command bound to a duplicate is retargeted to the kept gesture first; one whose group already binds the
    /// kept gesture (one command per trigger per group) is unbound instead, never left pointing at a deleted gesture.
    /// </summary>
    private void KeepThis(GestureTileItem tile)
    {
        var losers = tile.Partners.Select(partner => partner.Id).ToHashSet();
        if (losers.Count == 0)
        {
            Message = $"'{tile.Name}' has no duplicates.";
            return;
        }

        var retargeted = 0;
        var unbound = 0;
        foreach (var loser in losers)
        {
            var moved = _bindings.Retarget(loser, tile.Id);
            retargeted += moved.Retargeted;
            unbound += moved.Unbound;
        }

        var names = _library.All.Where(gesture => losers.Contains(gesture.Id)).Select(gesture => "'" + gesture.Name + "'").ToList();
        _library.ReplaceAll(_library.All.Where(gesture => !losers.Contains(gesture.Id)));
        if (retargeted + unbound == 0)
        {
            Message = $"Kept '{tile.Name}'; deleted {string.Join(", ", names)}. {GestureGridKeymap.Undo} undoes it.";
            return;
        }

        var commands = $"{Count(retargeted, "command")} retargeted to '{tile.Name}'" + (unbound == 0 ? "." : $"; {Count(unbound, "command")} unbound (its group already used '{tile.Name}').");
        Message = $"Kept '{tile.Name}'; deleted {string.Join(", ", names)}. {commands} {GestureGridKeymap.Undo} undoes the delete; the Commands tab undoes the retargeting.";
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
