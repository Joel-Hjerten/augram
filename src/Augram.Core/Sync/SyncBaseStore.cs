using System.Globalization;
using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// The local sync state under <c>&lt;configFolder&gt;/sync/state/</c> (README: base state): one
/// <see cref="SyncMachineState"/> per other machine in <c>machines/&lt;id&gt;.json</c>, and this machine's
/// newest <see cref="KeepPublished"/> published revisions in <c>published/&lt;sequence&gt;-&lt;revision&gt;.json</c>
/// (a zero-padded counter, so the name sorts by age whatever the clock does). Every write goes to a temp file
/// first and replaces atomically. A file that cannot be read is reported through the notice callback and treated
/// as absent: the merge then has no base for that machine and reports conflicts rather than guessing. Used from
/// the sync worker only; not synchronised.
/// </summary>
public sealed class SyncBaseStore
{
    public const int KeepPublished = 20;
    private const int SequenceDigits = 10;

    private readonly Action<string>? _notice;

    /// <param name="configFolder">The config folder (<c>DefaultConfigFolder</c> in the app); the state goes under <c>sync/state</c>.</param>
    /// <param name="notice">Receives one line per state file that could not be read.</param>
    public SyncBaseStore(string configFolder, Action<string>? notice = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);
        Folder = Path.Combine(configFolder, "sync", "state");
        _notice = notice;
    }

    public string Folder { get; }

    /// <summary>True before this machine has synced with anything: no state for any machine and nothing published.</summary>
    public bool IsEmpty => !Files(MachinesFolder).Any() && !Files(PublishedFolder).Any();

    private string MachinesFolder => Path.Combine(Folder, "machines");

    private string PublishedFolder => Path.Combine(Folder, "published");

    /// <summary>
    /// True after a publish failed: the next run publishes a new revision even when nothing changed, so a commit
    /// the repository could not push goes out with it.
    /// </summary>
    public bool PublishPending
    {
        get => File.Exists(PublishPendingPath);
        set
        {
            if (value)
            {
                WriteAtomically(PublishPendingPath, "The last publish of this machine's file failed; the next sync publishes again.");
            }
            else if (File.Exists(PublishPendingPath))
            {
                File.Delete(PublishPendingPath);
            }
        }
    }

    private string PublishPendingPath => Path.Combine(Folder, "publish-pending.txt");

    /// <summary>
    /// The newest sync format this machine has published (README: format version); 0 before a build that records it
    /// first published. A build that writes an older format pauses instead of rewriting this machine's file in it.
    /// </summary>
    public int PublishedFormatVersion
        => File.Exists(PublishedFormatPath)
            && int.TryParse(File.ReadAllText(PublishedFormatPath).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int version)
            ? version
            : 0;

    private string PublishedFormatPath => Path.Combine(Folder, "published-format.txt");

    /// <summary>The state of every machine this one has merged with (unreadable files skipped and reported).</summary>
    public IReadOnlyList<SyncMachineState> Machines()
        => Files(MachinesFolder).Select(path => Read(path, SyncStateJson.ReadMachine)).OfType<SyncMachineState>().ToArray();

    public SyncMachineState? Machine(Guid machineId)
    {
        var path = MachinePath(machineId);
        return File.Exists(path) ? Read(path, SyncStateJson.ReadMachine) : null;
    }

    /// <summary>Every conflict waiting for the user, across machines.</summary>
    public IReadOnlyList<SyncConflict> PendingConflicts() => Machines().SelectMany(state => state.Conflicts).ToArray();

    public void Save(SyncMachineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WriteAtomically(MachinePath(state.MachineId), SyncStateJson.Write(state));
    }

    /// <summary>The published revision with this id, or null when it is unknown here (never published, or pruned).</summary>
    public SyncPublished? Published(Guid revision)
    {
        var path = Files(PublishedFolder).FirstOrDefault(file => file.EndsWith($"-{revision:D}.json", StringComparison.OrdinalIgnoreCase));
        return path is null ? null : Read(path, SyncStateJson.ReadPublished);
    }

    /// <summary>The newest published revision, or null before the first publish.</summary>
    public SyncPublished? LastPublished()
    {
        var newest = Files(PublishedFolder).Order(StringComparer.Ordinal).LastOrDefault();
        return newest is null ? null : Read(newest, SyncStateJson.ReadPublished);
    }

    /// <summary>Records a publish in sync format <paramref name="version"/>; never lowers <see cref="PublishedFormatVersion"/>.</summary>
    public void RaisePublishedFormatVersion(int version)
    {
        if (version > PublishedFormatVersion)
        {
            WriteAtomically(PublishedFormatPath, version.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Stores a revision about to be published and prunes all but the newest <see cref="KeepPublished"/>.</summary>
    public void Save(SyncPublished published)
    {
        ArgumentNullException.ThrowIfNull(published);
        long next = Files(PublishedFolder).Select(Sequence).DefaultIfEmpty(0).Max() + 1;
        var name = $"{next.ToString(CultureInfo.InvariantCulture).PadLeft(SequenceDigits, '0')}-{published.Revision:D}.json";
        WriteAtomically(Path.Combine(PublishedFolder, name), SyncStateJson.Write(published));
        foreach (var old in Files(PublishedFolder).Order(StringComparer.Ordinal).SkipLast(KeepPublished))
        {
            File.Delete(old);
        }
    }

    /// <summary>
    /// Forgets everything this machine knew about the repository it synced with: every machine's state, the
    /// published revisions, the pending-publish flag and the published format (leftover temp files too), so <see cref="IsEmpty"/> is
    /// true and the next run asks the join question again. The App calls it when the repository URL changes or
    /// is cleared. Deletes files one by one inside <see cref="Folder"/>; the folders themselves stay.
    /// </summary>
    public void Clear()
    {
        foreach (var folder in new[] { MachinesFolder, PublishedFolder, Folder })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder).Where(IsStateFile).ToArray())
            {
                File.Delete(file);
            }
        }
    }

    private static bool IsStateFile(string path)
        => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private string MachinePath(Guid machineId) => Path.Combine(MachinesFolder, $"{machineId:D}.json");

    private static long Sequence(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > SequenceDigits
            && long.TryParse(name.AsSpan(0, SequenceDigits), NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
            ? sequence
            : 0;
    }

    private static IEnumerable<string> Files(string folder)
        => Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json") : [];

    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private T? Read<T>(string path, Func<string, T> parse)
        where T : class
    {
        try
        {
            return parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConfigFormatException)
        {
            _notice?.Invoke($"Sync state {path} ignored: {ex.Message}");
            return null;
        }
    }
}
