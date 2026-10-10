using System.ComponentModel;
using System.Globalization;
using Augram.App.Import;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Import.StrokesPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.ViewModels;

/// <summary>
/// The StrokesPlus.net import dialog (F8, plan 0001 M2 step 8): reads a file through
/// <see cref="StrokesPlusImporter.ReadAll(Stream)"/>, shows the stats line, the warnings and the
/// merge plan with one <see cref="ImportConflictItem"/> per name clash or shape match, and applies it:
/// the gestures through <see cref="GestureMerge"/> into the library, then the mapping, rebound to the
/// ids the gesture merge settled on, through <see cref="MappingImport"/> into the store. Merge policy
/// and validation stay in the importer and the stores.
/// </summary>
public sealed partial class ImportViewModel : ObservableObject
{
    public const string LogSource = "import";
    public const string LogMessage = "Imported from StrokesPlus.net";
    private const string SourceFileName = "StrokesPlus.net.json";

    private readonly GestureLibrary _library;
    private readonly MappingStore _mapping;
    private readonly IEventLog _log;
    private MergePlan? _plan;
    private ImportResult? _result;

    public ImportViewModel(GestureLibrary library, MappingStore mapping, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(log);
        _library = library;
        _mapping = mapping;
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
            Load(StrokesPlusImporter.ReadAll(stream));
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
        foreach (var conflict in Conflicts)
        {
            conflict.PropertyChanged -= OnChoiceChanged;
        }

        _result = result;
        _plan = GestureMerge.Plan(_library.All, result.Gestures, RecognitionOptions.Default);
        StatsText = string.Create(
            CultureInfo.InvariantCulture,
            $"Found {result.Stats.GestureCount} gestures, {result.AppGroupCount} app groups, {result.CommandCount} commands ({result.PlaceholderStepCount} steps as placeholders until their step types exist), {result.IgnoredAppCount} excluded apps.");
        Warnings = result.Warnings.Select(warning => $"{warning.Severity}: {warning.Item}: {warning.Message}").ToList();
        Conflicts = _plan.Conflicts.Select(entry => new ImportConflictItem(entry)).ToList();
        foreach (var conflict in Conflicts)
        {
            conflict.PropertyChanged += OnChoiceChanged;
        }

        Error = null;
        UpdatePlanText();
        CanApply = _plan.Entries.Count > 0 || result.CommandCount > 0 || result.AppGroupCount > 0 || result.IgnoredAppCount > 0;
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

        var choices = ChoicesById();
        var gestures = GestureMerge.ApplyWithMap(_plan, choices);
        MappingMergeResult mapping;
        try
        {
            // The mapping is merged and validated before either store changes, so a refusal leaves both untouched.
            // Two stores means two undo steps (one per store); accepted for this slice rather than a cross-store transaction.
            mapping = MappingImport.Merge(_mapping.Current, MappingImport.Rebind(_result.Mapping, gestures.IdMap));
            _library.ReplaceAll(gestures.Gestures);
            _mapping.ReplaceAll(mapping.Document);
        }
        catch (Exception exception) when (exception is GestureValidationException or MappingValidationException)
        {
            Error = exception.Message;
            return;
        }

        _log.Info(
            LogSource,
            LogMessage,
            ("source", SourcePath),
            ("found", _result.Stats.GestureCount),
            ("added", _plan.Entries.Count - Conflicts.Count + choices.Count(pair => pair.Value == MergeChoice.KeepBoth)),
            ("replaced", choices.Count(pair => pair.Value == MergeChoice.TakeTheirs)),
            ("kept", choices.Count(pair => pair.Value == MergeChoice.KeepMine)),
            ("groupsAdded", mapping.GroupsAdded),
            ("commandsAdded", mapping.CommandsAdded),
            ("commandsSkipped", mapping.CommandsSkipped),
            ("ignoredAdded", mapping.IgnoredAdded),
            ("ignoredSkipped", mapping.IgnoredSkipped),
            ("placeholderSteps", _result.PlaceholderStepCount),
            ("warnings", _result.Warnings.Count),
            ("libraryCount", _library.All.Count));
        CanApply = false;
        Finished?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Cancel() => Finished?.Invoke(this, EventArgs.Empty);

    private void OnChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportConflictItem.SelectedChoice))
        {
            UpdatePlanText();
        }
    }

    /// <summary>The plan line depends on the choices: a KeepMine binds imported commands to the existing gesture, which may already be bound in that group.</summary>
    private void UpdatePlanText()
    {
        if (_plan is null || _result is null)
        {
            return;
        }

        var additions = _plan.Entries.Count - Conflicts.Count;
        try
        {
            var gestures = GestureMerge.ApplyWithMap(_plan, ChoicesById());
            var mapping = MappingImport.Merge(_mapping.Current, MappingImport.Rebind(_result.Mapping, gestures.IdMap));
            PlanText = string.Create(
                CultureInfo.InvariantCulture,
                $"{additions} new gesture(s) will be added; {Conflicts.Count} already exist by name or shape. {mapping.GroupsAdded} group(s) and {mapping.CommandsAdded} command(s) will be added; {mapping.CommandsSkipped} command(s) already exist and are skipped; {mapping.IgnoredAdded} excluded app(s) will be added.");
        }
        catch (MappingValidationException exception)
        {
            Error = exception.Message;
        }
    }

    private Dictionary<GestureId, MergeChoice> ChoicesById()
        => Conflicts.ToDictionary(conflict => conflict.ImportedId, conflict => conflict.Choice);
}
