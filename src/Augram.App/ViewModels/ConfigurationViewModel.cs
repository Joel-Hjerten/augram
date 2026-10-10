using Augram.App.Import;
using Augram.App.Transfer;
using Augram.Core.Transfer;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Options › Configuration (plan 0003, Question 4 as proposed): Export… (everything preselected) and the StrokesPlus.net
/// import beside it (it stays on the Gestures toolbar too). Forwards each to its presenter and keeps the last outcome line
/// for the page; the flows themselves are the presenters'.
/// </summary>
public sealed partial class ConfigurationViewModel : ObservableObject
{
    private readonly IExportPresenter _export;
    private readonly IImportPresenter _strokesPlus;

    public ConfigurationViewModel(IExportPresenter export, IImportPresenter strokesPlus)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(strokesPlus);
        _export = export;
        _strokesPlus = strokesPlus;
    }

    /// <summary>What the last export or import did ("Exported options, 90 gestures, … to Augram everything 2026-10-10.augram.json."); empty before the first.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string Status { get; private set; } = string.Empty;

    public bool HasStatus => Status.Length > 0;

    public Task ExportAsync() => RunAsync(() => _export.ExportAsync(ExportScope.Everything));

    public Task ImportStrokesPlusAsync() => RunAsync(async () =>
    {
        await _strokesPlus.OpenAsync().ConfigureAwait(true);
        return null;
    });

    private async Task RunAsync(Func<Task<string?>> flow)
    {
        try
        {
            if (await flow().ConfigureAwait(true) is { } outcome)
            {
                Status = outcome;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = "Failed: " + exception.Message;
        }
    }
}
