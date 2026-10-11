using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// What importing a file would do to this configuration (plan 0003): an import is the sync's merge without a base. The
/// file is lined up by <see cref="ImportMatcher"/> (ids kept; an unknown id matched by name, hold key or shape) and split into
/// sync items; each item is <see cref="ImportStatus.New"/> (added), <see cref="ImportStatus.Same"/> (nothing to do) or
/// <see cref="ImportStatus.Different"/> (a <see cref="SyncConflict"/>, Keep mine until chosen). Nothing is ever deleted: an
/// item the file lacks is not in the plan. The Global group of a file that did not export it is an empty shell and is left
/// out entirely. A command's own steps follow their command when it differs, so they are no entry then. Pure; the result
/// of <see cref="Resolve"/> is applied by <see cref="ImportResult.ApplyTo"/> (<see cref="ImportApplier"/> plans again when the
/// stores moved). Make the plan from the stores' snapshots (<see cref="ConfigSession.Document"/>), so that applying can tell
/// whether they moved meanwhile.
/// </summary>
public sealed class ImportPlan
{
    private readonly ConfigDocument _current;
    private readonly SyncItemSet _local;
    private readonly SyncItemSet _items;
    private readonly Dictionary<SyncItemKey, SyncConflict> _conflicts = [];
    private readonly Dictionary<SyncItemKey, ImportStatus> _statuses = [];
    private ImportResult? _preview;

    private ImportPlan(TransferFile file, ConfigDocument current, SyncItemSet items, IReadOnlyDictionary<SyncItemKey, ImportMatch> matches, string sourceName)
    {
        File = file;
        _current = current;
        _local = SyncItemSet.From(current.Gestures, current.Mapping);
        _items = items;

        var entries = new List<ImportEntry>();
        var differingCommands = new HashSet<CommandId>();
        foreach (var item in items)
        {
            if (item is SyncItem.VersionItem version && differingCommands.Contains(version.CommandId))
            {
                continue;
            }

            var mine = _local.Find(item.Key);
            var status = mine is null ? ImportStatus.New
                : string.Equals(mine.Content, item.Content, StringComparison.Ordinal) ? ImportStatus.Same
                : ImportStatus.Different;
            entries.Add(new ImportEntry(item.Key, mine?.Name ?? item.Name, status) { Match = matches.GetValueOrDefault(item.Key) });
            _statuses[item.Key] = status;
            if (status == ImportStatus.Different)
            {
                _conflicts[item.Key] = new SyncConflict(item.Key, mine!.Name, mine.Content, item.Content) { MachineName = sourceName };
                if (item is SyncItem.CommandItem command)
                {
                    differingCommands.Add(command.Command.Id);
                }
            }
        }

        Entries = entries;
        Conflicts = [.. _conflicts.Values];
        SettingsDiffer = file.Settings is { } theirs && WithOptionsFrom(current.Settings, theirs) != current.Settings;
    }

    /// <summary>The file as read.</summary>
    public TransferFile File { get; }

    /// <summary>One entry per item of the file, in document order (gestures, then each group's header, categories, hold remaps, commands, then ignored apps).</summary>
    public IReadOnlyList<ImportEntry> Entries { get; }

    /// <summary>Every <see cref="ImportStatus.Different"/> entry as the sync's conflict record (<see cref="SyncConflict.MachineName"/> is the source's name), for the choices.</summary>
    public IReadOnlyList<SyncConflict> Conflicts { get; }

    /// <summary>The file carries options (decision 2: "Also use its options" is offered).</summary>
    public bool HasSettings => File.Settings is not null;

    /// <summary>Taking the file's options would change something here.</summary>
    public bool SettingsDiffer { get; }

    /// <summary>Everything in the file is already here as it is: nothing to import.</summary>
    public bool IsEmpty => !SettingsDiffer && Entries.All(entry => entry.Status == ImportStatus.Same);

    /// <summary>Every choice Keep mine, options left alone: what the review shows before the user chooses.</summary>
    public ImportResult Preview => _preview ??= Resolve(ImportChoices.KeepMine);

    /// <param name="file">The file, read by <see cref="TransferSerializer"/>.</param>
    /// <param name="current">The configuration here: the stores' snapshots (<see cref="ConfigSession.Document"/>).</param>
    /// <param name="options">How to match and what to call the file's side; <see cref="ImportOptions.Default"/> when null.</param>
    public static ImportPlan Create(TransferFile file, ConfigDocument current, ImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(current);
        options ??= ImportOptions.Default;

        var matched = ImportMatcher.Match(file, current.Gestures, current.Mapping, options.MatchShapes);
        var mapping = matched.Mapping ?? MappingDocument.Empty;
        var global = SyncItemKey.ForGroup(GroupId.Global);
        bool globalIsShell = mapping.Groups.FirstOrDefault(group => group.IsGlobal) is not { } fileGlobal || fileGlobal.IsShell;
        var items = new SyncItemSet(SyncItemSet.From(matched.Gestures, mapping).Where(item => !(globalIsShell && item.Key == global)));
        return new ImportPlan(file, current, items, matched.Matches, options.SourceName);
    }

    public int Count(ImportStatus status) => Entries.Count(entry => entry.Status == status);

    /// <summary>
    /// The configuration the import leaves under <paramref name="choices"/>, repaired and validated, with what changed (plan 0003,
    /// decisions 6–8): the sync's <see cref="ConflictResolution"/> over every item of the file by its status: a New one taken, a
    /// Same one skipped, a Different one by its effective choice (<see cref="SyncChoiceRules.Effective"/>, the sync's: Keep both
    /// only for gestures and commands, else the nearest choice with a note), a differing command's own steps following it.
    /// </summary>
    public ImportResult Resolve(ImportChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var notes = new List<string>();
        var answers = new List<ConflictResolution.Answer>();
        foreach (var item in _items)
        {
            if (!_statuses.TryGetValue(item.Key, out var status))
            {
                answers.Add(new ConflictResolution.Answer(item.Key, item, SyncChoice.KeepMine) { FollowsCommand = true });
            }
            else if (status == ImportStatus.New)
            {
                answers.Add(new ConflictResolution.Answer(item.Key, item, SyncChoice.TakeTheirs));
            }
            else if (status == ImportStatus.Different)
            {
                var (choice, note) = SyncChoiceRules.Effective(_conflicts[item.Key], choices.For(item.Key));
                if (note is not null)
                {
                    notes.Add(note);
                }

                answers.Add(new ConflictResolution.Answer(item.Key, item, choice));
            }
        }

        var built = ConflictResolution.Apply(_local, answers, _items);
        var settings = choices.TakeSettings && File.Settings is { } theirs
            ? WithOptionsFrom(_current.Settings, theirs)
            : _current.Settings;
        return new ImportResult(built.Gestures, built.Mapping, settings, SyncCounts.Between(_local, built.Items), built.Repairs, notes, _current);
    }

    /// <summary>
    /// Decision 2: the file's preferences over this machine's options. Capture, trail, recognition and no-match whole, and the
    /// stroke button and ignore keys of General; this machine keeps its sync section and the rest of General (start at login,
    /// enabled, the menu-bar icon), which are its state, not preferences, and its appearance (plan 0006), a preference of its own.
    /// </summary>
    internal static Settings WithOptionsFrom(Settings current, Settings file) => current with
    {
        General = current.General with { StrokeButton = file.General.StrokeButton, IgnoreKey = file.General.IgnoreKey },
        Capture = file.Capture,
        Trail = file.Trail,
        Recognition = file.Recognition,
        NoMatch = file.NoMatch,
    };
}
