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
/// of <see cref="Resolve"/> is applied by <see cref="ImportResult.ApplyTo"/>. Make the plan from the stores' snapshots
/// (<see cref="ConfigSession.Document"/>), so that applying can tell whether they moved meanwhile.
/// </summary>
public sealed class ImportPlan
{
    private readonly ConfigDocument _current;
    private readonly SyncItemSet _local;
    private readonly SyncItemSet _items;
    private readonly Dictionary<SyncItemKey, SyncConflict> _conflicts = [];
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
        SettingsDiffer = file.Settings is { } theirs && ImportResolution.WithOptionsFrom(current.Settings, theirs) != current.Settings;
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
        bool globalIsShell = mapping.Groups.FirstOrDefault(group => group.IsGlobal) is not { } fileGlobal || TransferContents.IsShell(fileGlobal);
        var items = new SyncItemSet(SyncItemSet.From(matched.Gestures, mapping).Where(item => !(globalIsShell && item.Key == global)));
        return new ImportPlan(file, current, items, matched.Matches, options.SourceName);
    }

    public int Count(ImportStatus status) => Entries.Count(entry => entry.Status == status);

    /// <summary>The configuration the import leaves under <paramref name="choices"/>, repaired and validated, with what changed.</summary>
    public ImportResult Resolve(ImportChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var (built, notes) = ImportResolution.Resolve(_local, _items, _conflicts, choices);
        var settings = choices.TakeSettings && File.Settings is { } theirs
            ? ImportResolution.WithOptionsFrom(_current.Settings, theirs)
            : _current.Settings;
        return new ImportResult(built.Gestures, built.Mapping, settings, SyncCounts.Between(_local, built.Items), built.Repairs, notes, _current);
    }
}
