using Augram.App.Dialogs;
using Augram.App.Hosting;
using Augram.App.Transfer;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Transfer;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The plumbing of a tab that projects a store and turns intents into store calls (ADR-0002 §5a): the Commands, Gestures and
/// Exclusions view models. It holds the message line (<see cref="Message"/>), runs an intent and shows a refusal there
/// (<see cref="Guard"/>, over the exceptions the view model names: <see cref="MappingRefusals"/> or
/// <see cref="GestureRefusals"/>), brings a store change to the UI thread (<see cref="OnStoreChanged"/> →
/// <see cref="OnStoreChangedOnUi"/>), deletes after a confirmation (<see cref="DeleteAsync"/>) and runs the export dialog
/// (<see cref="ExportAsync"/>). The view model subscribes <see cref="OnStoreChanged"/> to its stores and unsubscribes on Dispose.
/// </summary>
public abstract partial class StoreViewModel : ObservableObject
{
    /// <summary>A rule the mapping store refused, or an item gone meanwhile: the Commands and Exclusions tabs.</summary>
    public static readonly Func<Exception, bool> MappingRefusals = exception => exception is MappingValidationException or KeyNotFoundException;

    /// <summary>A rule the gesture library or the mapping store refused: the Gestures tab.</summary>
    public static readonly Func<Exception, bool> GestureRefusals = exception => exception is GestureValidationException or MappingValidationException;

    private readonly Func<Exception, bool> _isRefusal;
    private readonly IExportPresenter? _export;

    /// <param name="confirm">Asks before a delete.</param>
    /// <param name="isRefusal">Which exceptions <see cref="Guard"/> shows as the message instead of throwing.</param>
    /// <param name="export">Runs the export dialog; null where there is none (tests, the gallery).</param>
    protected StoreViewModel(IConfirmPresenter confirm, Func<Exception, bool> isRefusal, IExportPresenter? export = null)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(isRefusal);
        Confirm = confirm;
        _isRefusal = isRefusal;
        _export = export;
    }

    /// <summary>Feedback for the last action: a rule message, or "Deleted 'X'. Ctrl+Z undoes it."</summary>
    [ObservableProperty]
    public partial string? Message { get; protected set; }

    protected IConfirmPresenter Confirm { get; }

    /// <summary>Runs an intent; a refusal (an exception <c>isRefusal</c> names) shows its message. False when it was refused.</summary>
    protected bool Guard(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception) when (_isRefusal(exception))
        {
            Message = exception.Message;
        }

        return false;
    }

    /// <summary>
    /// Asks <paramref name="question"/> under <paramref name="title"/> with a Delete button (null: deletes at once, without
    /// asking); on yes runs <paramref name="remove"/> through <see cref="Guard"/>, and the line it returns is the message.
    /// </summary>
    protected async Task DeleteAsync(string title, string? question, Func<string> remove)
    {
        ArgumentNullException.ThrowIfNull(remove);
        if (question is not null && !await Confirm.ConfirmAsync(title, question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() => Message = remove());
    }

    /// <summary>The line after a delete: "Deleted 'X'. Ctrl+Z undoes it.", with the tab's undo key.</summary>
    protected static string Deleted(string name, string undo) => $"Deleted '{name}'. {undo} undoes it.";

    /// <summary>The export dialog with <paramref name="scope"/> preselected (plan 0003); its outcome goes to the message line, Cancel leaves it alone.</summary>
    protected async Task ExportAsync(ExportScope scope)
    {
        if (_export is null)
        {
            Message = "Export is not available here.";
            return;
        }

        if (await _export.ExportAsync(scope).ConfigureAwait(true) is { } outcome)
        {
            Message = outcome;
        }
    }

    /// <summary>A store's <c>Changed</c> handler, from any thread: <see cref="OnStoreChangedOnUi"/> at once on the UI thread, else posted there.</summary>
    protected void OnStoreChanged(object? sender, EventArgs e) => UiThread.RunOrPost(OnStoreChangedOnUi);

    /// <summary>Re-reads the store on the UI thread.</summary>
    protected abstract void OnStoreChangedOnUi();
}
