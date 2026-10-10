using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Transfer;

namespace Augram.App.Transfer;

/// <summary>
/// The export as the user sees it (plan 0003 step 3): <see cref="ExportViewModel"/>'s form in the shared
/// <see cref="FormDialog"/> (Save… disabled while a selection is empty), then the save picker with
/// <see cref="Exporter.SuggestedFileName"/>, then <see cref="TransferSerializer.Write"/> to that path through a temp file
/// and a move, as the config file is written. Logs Info <c>export</c> with the scope and the counts, never content. What a
/// scope holds is Core's; nothing here decides it.
/// </summary>
public sealed class ExportPresenter : IExportPresenter
{
    public const string LogSource = "export";
    public const string LogMessage = "Exported Augram file";

    private readonly ConfigSession _session;
    private readonly IFormDialogPresenter _dialogs;
    private readonly ITransferFilePicker _picker;
    private readonly IEventLog _log;
    private readonly Func<DateOnly> _today;

    /// <summary><c>today</c> is the date in the suggested file name: the local date unless a test says otherwise.</summary>
    public ExportPresenter(ConfigSession session, IFormDialogPresenter dialogs, ITransferFilePicker picker, IEventLog log, Func<DateOnly>? today = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(log);
        _session = session;
        _dialogs = dialogs;
        _picker = picker;
        _log = log;
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
    }

    public async Task<string?> ExportAsync(ExportScope start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var viewModel = new ExportViewModel(_session.Document, start);
        if (!await _dialogs.ShowAsync(Request(viewModel)).ConfigureAwait(true) || !viewModel.CanSave)
        {
            return null;
        }

        var path = await _picker.PickSaveAsync(viewModel.SuggestedFileName(_today())).ConfigureAwait(true);
        if (path is null)
        {
            return null;
        }

        var file = viewModel.Export();
        var contents = TransferContents.Of(file);
        var name = Path.GetFileName(path);
        try
        {
            await WriteAsync(path, TransferSerializer.Write(file)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Warning(LogSource, "Could not write Augram file", ("error", exception.Message));
            return $"Could not save {name}: {exception.Message}";
        }

        _log.Info(LogSource, LogMessage, ("scope", viewModel.Kind), ("contents", contents.ToString()), ("privateTextSteps", contents.PrivateTextSteps));
        return $"Exported {contents} to {name}.";
    }

    /// <summary>The scope dialog for <paramref name="viewModel"/>: its form, Save… enabled while there is something to export.</summary>
    public static FormDialogRequest Request(ExportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new FormDialogRequest(ExportViewModel.Title, ExportViewModel.ConfirmLabel, Screen: viewModel.Declare())
        {
            CanConfirm = new DelegateBinding<bool>(() => viewModel.CanSave, owner: viewModel, propertyName: nameof(viewModel.CanSave)),
        };
    }

    /// <summary>As the config file is written: a temp file beside the target, then a move over it, so a failed write never leaves half a file.</summary>
    public static async Task WriteAsync(string path, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(json);
        var temp = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, json).ConfigureAwait(true);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: the export already failed and says why; a stray .tmp is harmless.
        }
    }
}
