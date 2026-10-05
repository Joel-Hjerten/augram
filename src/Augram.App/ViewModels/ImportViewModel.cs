using System.Globalization;
using Augram.App.Import;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Import.StrokesPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.ViewModels;

/// <summary>
/// The StrokesPlus.net import dialog (F8, plan 0001 M1 step 10): reads a file through
/// <see cref="StrokesPlusImporter"/>, shows the stats line, the warnings and the merge plan with one
/// <see cref="ImportConflictItem"/> per clash, and applies it through <see cref="GestureMerge"/> into
/// the library as one undo step. Merge policy and validation stay in the importer and the library.
/// </summary>
public sealed partial class ImportViewModel : ObservableObject
{
    public const string LogSource = "import";
    private const string SourceFileName = "StrokesPlus.net.json";

    private readonly GestureLibrary _library;
    private readonly IEventLog _log;
    private MergePlan? _plan;
    private ImportResult? _result;

    public ImportViewModel(GestureLibrary library, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(log);
        _library = library;
        _log = log;
    }

    /// <summary>Raised after Apply or Cancel; the dialog closes on it.</summary>
    public event EventHandler? Finished;

    /// <summary>SP.net's live config, when this machine has one; the file picker starts there.</summary>
    public static string? DefaultSourcePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (appData.Length == 0)
            {
                return null;
            }

            var path = Path.Combine(appData, "StrokesPlus.net", SourceFileName);
            return File.Exists(path) ? path : null;
        }
    }

    public static string SuggestedFileName => SourceFileName;

    [ObservableProperty]
    public partial string? SourcePath { get; private set; }

    [ObservableProperty]
    public partial string StatsText { get; private set; } = "No file loaded.";

    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ImportConflictItem> Conflicts { get; private set; } = [];

    [ObservableProperty]
    public partial string PlanText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool CanApply { get; private set; }

    public bool HasConflicts => Conflicts.Count > 0;

    public bool HasWarnings => Warnings.Count > 0;

    public IReadOnlyList<Declarations.Choice<MergeChoice>> Choices => ImportConflictItem.Choices;

    public void Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SourcePath = path;
        try
        {
            using var stream = File.OpenRead(path);
            Load(StrokesPlusImporter.ReadGestures(stream));
        }
        catch (Exception exception) when (exception is ImportFormatException or IOException or UnauthorizedAccessException)
        {
            Error = exception.Message;
            CanApply = false;
        }
    }

    public void Load(ImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
        _plan = GestureMerge.Plan(_library.All, result.Gestures);
        var stats = result.Stats;
        StatsText = string.Create(
            CultureInfo.InvariantCulture,
            $"Found {stats.GestureCount} gestures, {stats.SampleCount} samples; {stats.ActionCount ?? 0} actions and {stats.ApplicationCount ?? 0} apps will import in a later version.");
        Warnings = result.Warnings.Select(warning => $"{warning.Severity}: {warning.Item}: {warning.Message}").ToList();
        Conflicts = _plan.Conflicts.Select(entry => new ImportConflictItem(entry)).ToList();
        var additions = _plan.Entries.Count - Conflicts.Count;
        PlanText = string.Create(CultureInfo.InvariantCulture, $"{additions} new gesture(s) will be added; {Conflicts.Count} name(s) already exist.");
        Error = null;
        CanApply = _plan.Entries.Count > 0;
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public void ApplyToAll(MergeChoice choice)
    {
        foreach (var conflict in Conflicts)
        {
            conflict.Choice = choice;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    public void Apply()
    {
        if (_plan is null || _result is null)
        {
            return;
        }

        var choices = Conflicts.ToDictionary(conflict => conflict.ImportedId, conflict => conflict.Choice);
        var merged = GestureMerge.Apply(_plan, choices);
        try
        {
            _library.ReplaceAll(merged);
        }
        catch (GestureValidationException exception)
        {
            Error = exception.Message;
            return;
        }

        _log.Info(
            LogSource,
            "Gestures imported from StrokesPlus.net",
            ("source", SourcePath),
            ("found", _result.Stats.GestureCount),
            ("added", _plan.Entries.Count - Conflicts.Count + choices.Count(pair => pair.Value == MergeChoice.KeepBoth)),
            ("replaced", choices.Count(pair => pair.Value == MergeChoice.TakeTheirs)),
            ("kept", choices.Count(pair => pair.Value == MergeChoice.KeepMine)),
            ("warnings", _result.Warnings.Count),
            ("libraryCount", _library.All.Count));
        CanApply = false;
        Finished?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Cancel() => Finished?.Invoke(this, EventArgs.Empty);
}
