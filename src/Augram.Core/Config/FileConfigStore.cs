namespace Augram.Core.Config;

/// <summary>
/// <c>&lt;folder&gt;/augram.json</c> with backup-before-write and a load fallback chain
/// (requirements F8). Save: back up the current file, write a temp file, replace atomically.
/// Load: main file, else the newest backup that parses, else <see cref="ConfigDocument.Default"/>;
/// each fallback is reported through the notice callback (the event log is wired in by the host).
/// </summary>
public sealed class FileConfigStore : IConfigStore
{
    public const string FileName = "augram.json";
    public const string BackupFolderName = "backup";

    private readonly Action<string>? _notice;
    private readonly Func<DateTime> _clock;

    /// <param name="folder">The config folder (checklist A17); created on first save.</param>
    /// <param name="notice">Receives one line per fallback or failure; null to stay silent.</param>
    /// <param name="clock">Local time for backup names; injectable for tests.</param>
    public FileConfigStore(string folder, Action<string>? notice = null, Func<DateTime>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Folder = folder;
        Backups = new ConfigBackups(Path.Combine(folder, BackupFolderName));
        _notice = notice;
        _clock = clock ?? (() => DateTime.Now);
    }

    public string Folder { get; }

    public ConfigBackups Backups { get; }

    public string Location => Path.Combine(Folder, FileName);

    public ConfigDocument Load()
    {
        if (!File.Exists(Location))
        {
            _notice?.Invoke($"No configuration at {Location}.");
        }
        else if (TryRead(Location, out var document))
        {
            return document;
        }

        foreach (var backup in Backups.NewestFirst())
        {
            if (TryRead(backup, out var document))
            {
                _notice?.Invoke($"Loaded backup {backup}.");
                return document;
            }
        }

        _notice?.Invoke("Using built-in defaults with the starter gestures.");
        return ConfigDocument.Default;
    }

    public void Save(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var json = ConfigSerializer.Write(document);

        Directory.CreateDirectory(Folder);
        if (File.Exists(Location))
        {
            Backups.Add(Location, _clock());
            Backups.Prune();
        }

        var temp = Location + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, Location, overwrite: true);
    }

    private bool TryRead(string path, out ConfigDocument document)
    {
        try
        {
            document = ConfigSerializer.Read(File.ReadAllText(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConfigFormatException)
        {
            _notice?.Invoke($"Could not load {path}: {ex.Message}");
            document = ConfigDocument.Default;
            return false;
        }
    }
}
