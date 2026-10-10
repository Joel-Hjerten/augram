using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Transfer;

namespace Augram.App.Transfer;

/// <summary>
/// The import as the user sees it (plan 0003 step 4): the open picker ("Augram files (*.augram.json)", "JSON (*.json)"), then
/// <see cref="TransferSerializer.TryRead"/>: a file that cannot be read, was written by a newer Augram or breaks a rule is said
/// in a message with its one error line and nothing changes. Otherwise the review (<see cref="AugramImportViewModel"/>, modal),
/// which applies the import itself, and the summary with its repairs in a message. What is imported and how it merges is
/// Core's; nothing here decides it.
/// </summary>
public sealed class AugramImportPresenter : IAugramImportPresenter
{
    private readonly ConfigSession _session;
    private readonly ITransferFilePicker _picker;
    private readonly IAugramImportWindows _windows;
    private readonly IEventLog _log;
    private readonly StepRegistry _steps;

    /// <summary><c>steps</c> are the step types a file may use (<see cref="StepRegistry.BuiltIn"/> unless a test says otherwise).</summary>
    public AugramImportPresenter(ConfigSession session, ITransferFilePicker picker, IAugramImportWindows windows, IEventLog log, StepRegistry? steps = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(log);
        _session = session;
        _picker = picker;
        _windows = windows;
        _log = log;
        _steps = steps ?? StepRegistry.BuiltIn;
    }

    public async Task<string?> OpenAsync()
    {
        var path = await _picker.PickOpenAsync().ConfigureAwait(true);
        if (path is null)
        {
            return null;
        }

        var name = Path.GetFileName(path);
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return await RefuseAsync(name, exception.Message).ConfigureAwait(true);
        }

        if (!TransferSerializer.TryRead(json, _steps, out var file, out var error))
        {
            return await RefuseAsync(name, error).ConfigureAwait(true);
        }

        var review = new AugramImportViewModel(_session, file, name, _log);
        await _windows.ShowReviewAsync(review).ConfigureAwait(true);
        if (review.ResultText is not { } result)
        {
            return null;
        }

        var report = review.ResultDetails.Count == 0
            ? result
            : result + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, review.ResultDetails);
        await _windows.ShowMessageAsync(AugramImportViewModel.Title, report).ConfigureAwait(true);
        return result;
    }

    /// <summary>"Blender.augram.json was not imported. The file was written by a newer Augram (schema version 5); …", shown and returned.</summary>
    private async Task<string> RefuseAsync(string name, string reason)
    {
        _log.Warning(AugramImportViewModel.LogSource, "Could not import Augram file", ("error", reason));
        var line = $"{name} was not imported. {reason}";
        await _windows.ShowMessageAsync(AugramImportViewModel.Title, line).ConfigureAwait(true);
        return line;
    }
}
