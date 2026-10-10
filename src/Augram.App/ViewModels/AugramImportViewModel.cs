using Augram.App.Components.SyncConflictList;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The review of an Augram file before it is imported (requirements F8; plan 0003 step 4): what the file holds, what is new,
/// the same or different here, what was matched by name or shape, the differing items with a choice each (through
/// <see cref="SyncConflictsViewModel"/>, Keep mine by default) and "apply to all", "Also use its options" when the file has
/// some (off by default), the repairs and notes the result would need under the current choices (recomputed on every
/// change), and the file's notices. <see cref="Import"/> applies it: the plan the user reviewed, or, when the stores moved
/// meanwhile (a sync), a new plan from them with the same choices, which are keyed by item and so carry over. The plan, the
/// merge and the repairs are Core's (<see cref="ImportPlan"/>); this shows them and forwards the choices.
/// </summary>
public sealed partial class AugramImportViewModel : ObservableObject
{
    public const string LogSource = "import";
    public const string LogMessage = "Imported Augram file";
    public const string Title = "Import Augram file";
    public const string ImportLabel = "Import";
    public const string EmptyText = "Everything in this file is already here.";
    public const string MineHeader = "Yours";
    public const string TheirsHeader = "In the file";

    public const string Help =
        "Each row is in the file and here, with different content. Keep mine leaves yours; Take theirs uses the file's; Keep both keeps yours and adds the file's beside it (gestures and commands). Everything new in the file is added; nothing of yours is deleted. Undo on the Gestures and Commands tabs takes the import back.";

    public const string OptionsHelp =
        "The file's stroke button, ignore keys, capture, trail, recognition and no-match choice. Not Sync, start at login, Enabled or the menu-bar icon: those stay this machine's.";

    /// <summary>How often <see cref="Import"/> plans again when the stores keep moving between planning and applying.</summary>
    private const int Attempts = 3;

    private readonly ConfigSession _session;
    private readonly TransferFile _file;
    private readonly IEventLog _log;
    private ImportPlan _plan;

    /// <summary><c>fileName</c> names the file's side in the rows and notes ("with Blender.augram.json").</summary>
    public AugramImportViewModel(ConfigSession session, TransferFile file, string fileName, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(log);
        _session = session;
        _file = file;
        _log = log;
        FileName = fileName;
        _plan = Plan();

        // Names in the rows come from the preview, which holds this configuration and everything new in the file.
        Conflicts = new SyncConflictsViewModel(_plan.Conflicts, _plan.Preview.Gestures, _plan.Preview.Mapping, help: Help);
        ConflictEntries = Conflicts.Entries;
        Preview = _plan.Preview;
    }

    /// <summary>Raised after a successful <see cref="Import"/> or a <see cref="Cancel"/>; the window closes on it.</summary>
    public event EventHandler? Finished;

    public string FileName { get; }

    /// <summary>The differing items and their choices.</summary>
    public SyncConflictsViewModel Conflicts { get; }

    /// <summary>What the conflict list shows; replaced when "apply to all" changes the choices.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SyncConflictEntry> ConflictEntries { get; private set; }

    /// <summary>"Also use its options" (plan 0003, decision 2): offered only when the file has options, off by default.</summary>
    [ObservableProperty]
    public partial bool TakeSettings { get; set; }

    /// <summary>What importing under the current choices would leave.</summary>
    public ImportResult Preview { get; private set; }

    /// <summary>After a successful import: "Imported from Blender.augram.json: 1 app group and 8 commands added."; null before.</summary>
    public string? ResultText { get; private set; }

    /// <summary>After a successful import: the repairs and notes it needed, one line each.</summary>
    public IReadOnlyList<string> ResultDetails { get; private set; } = [];

    /// <summary>Why the import did not happen, or empty.</summary>
    [ObservableProperty]
    public partial string Error { get; private set; } = string.Empty;

    public bool HasConflicts => _plan.Conflicts.Count > 0;

    public bool HasSettings => _plan.HasSettings;

    /// <summary>Everything in the file is here as it is (and its options, if any, are this machine's): nothing to import.</summary>
    public bool IsEmpty => _plan.IsEmpty;

    public bool CanImport => !IsEmpty && ResultText is null;

    public bool HasMatches => _plan.Entries.Any(entry => entry.Match is not null);

    public bool HasNotices => _file.Notices.Count > 0;

    public bool HasOutcome => Preview.Repairs.Count > 0 || Preview.Notes.Count > 0;

    public bool HasError => Error.Length > 0;

    /// <summary>"1 app group, 8 commands, 2 gestures": what the file holds.</summary>
    public string ContentsText => TransferContents.Of(_file).ToString();

    /// <summary>"New: 1 app group, 8 commands · Same as yours: 2 gestures · Different: 1 command".</summary>
    public string CountsText => Counts(_plan.Entries);

    /// <summary>One line per item matched by name, hold key or shape, at most <see cref="MatchLinesShown"/>.</summary>
    public string MatchesText => Matches(_plan.Entries);

    public string NoticesText => string.Join(Environment.NewLine, _file.Notices);

    /// <summary>The repairs and notes the result would need under the current choices, one per line.</summary>
    public string OutcomeText => string.Join(Environment.NewLine, Preview.Repairs.Select(repair => repair.Description).Concat(Preview.Notes));

    /// <summary>The choice for the differing item at <paramref name="index"/> (in <see cref="ConflictEntries"/> order); false when it does not offer it.</summary>
    public bool Choose(int index, SyncChoice choice)
    {
        if (!Conflicts.Choose(index, choice))
        {
            return false;
        }

        Refresh();
        return true;
    }

    /// <summary>"Apply to all": every differing item that offers <paramref name="choice"/> takes it (Keep both is for gestures and commands).</summary>
    public void ApplyToAll(SyncChoice choice)
    {
        if (Conflicts.ChooseAll(choice))
        {
            ConflictEntries = Conflicts.Entries;
            Refresh();
        }
    }

    /// <summary>The choices as Core takes them: one per differing item, and the options toggle.</summary>
    public ImportChoices Choices() => new()
    {
        Items = Conflicts.Resolutions().ToDictionary(resolution => resolution.Conflict.Key, resolution => resolution.Choice),
        TakeSettings = TakeSettings && HasSettings,
    };

    /// <summary>
    /// Applies the import on the stores' (UI) thread: one undo step per store that changes. The reviewed plan first; when a
    /// store moved since it was made (a sync applied while the review was open) the stores refuse it, and the import plans
    /// again from them with the same choices. Logs Info <c>import</c> with the counts, never content. False when there is
    /// nothing to import, or the stores kept moving (then <see cref="Error"/> says so and nothing changed).
    /// </summary>
    public bool Import()
    {
        if (!CanImport)
        {
            return false;
        }

        var choices = Choices();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0)
            {
                _plan = Plan();
            }

            var result = _plan.Resolve(choices);
            if (result.ApplyTo(_session.Settings, _session.Gestures, _session.Mapping))
            {
                Done(result, replanned: attempt);
                return true;
            }
        }

        Error = "Augram's configuration kept changing while importing (a sync?), so nothing was imported. Try again.";
        OnPropertyChanged(nameof(HasError));
        return false;
    }

    public void Cancel() => Finished?.Invoke(this, EventArgs.Empty);

    partial void OnTakeSettingsChanged(bool value) => Refresh();

    private ImportPlan Plan() => ImportPlan.Create(_file, _session.Document, new ImportOptions
    {
        SourceName = FileName,
        MatchShapes = _session.Settings.Current.Recognition,
    });

    /// <summary>The preview under the current choices, and every binding on the review told (an empty name refreshes them all).</summary>
    private void Refresh()
    {
        Preview = _plan.Resolve(Choices());
        OnPropertyChanged(string.Empty);
    }

    private void Done(ImportResult result, int replanned)
    {
        ResultText = Summary(FileName, result);
        ResultDetails = [.. result.Repairs.Select(repair => repair.Description), .. result.Notes];
        _log.Info(
            LogSource,
            LogMessage,
            ("counts", result.Counts.ToString()),
            ("options", result.SettingsChanged),
            ("repairs", result.Repairs.Count),
            ("notes", result.Notes.Count),
            ("replanned", replanned));
        OnPropertyChanged(string.Empty);
        Finished?.Invoke(this, EventArgs.Empty);
    }
}
