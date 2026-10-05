using System.Globalization;

namespace Augram.Core.Config;

/// <summary>
/// The <c>backup/</c> folder beside the config file: a dated copy of the previous file is
/// taken before every write and only the newest <see cref="Keep"/> are kept. Names are
/// <c>augram-yyyyMMdd-HHmmss.json</c>, with <c>-2</c>, <c>-3</c>... appended when several
/// saves land in one second, so lexical order of the stamp plus the counter is age order.
/// </summary>
public sealed class ConfigBackups
{
    public const int DefaultKeep = 20;
    private const string Prefix = "augram-";
    private const string Extension = ".json";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    public ConfigBackups(string folder, int keep = DefaultKeep)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentOutOfRangeException.ThrowIfLessThan(keep, 1);
        Folder = folder;
        Keep = keep;
    }

    public string Folder { get; }

    public int Keep { get; }

    /// <summary>Copies <paramref name="sourceFile"/> into the folder and returns the backup's path.</summary>
    public string Add(string sourceFile, DateTime timestamp)
    {
        Directory.CreateDirectory(Folder);
        var stamp = timestamp.ToString(StampFormat, CultureInfo.InvariantCulture);
        var path = Path.Combine(Folder, Prefix + stamp + Extension);
        for (int counter = 2; File.Exists(path); counter++)
        {
            path = Path.Combine(Folder, $"{Prefix}{stamp}-{counter}{Extension}");
        }

        File.Copy(sourceFile, path);
        return path;
    }

    /// <summary>Existing backups, newest first; an empty list when the folder does not exist.</summary>
    public IReadOnlyList<string> NewestFirst()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(Folder, Prefix + "*" + Extension)
            .Select(path => (Path: path, Key: AgeKey(Path.GetFileNameWithoutExtension(path))))
            .OrderByDescending(entry => entry.Key.Stamp, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.Key.Counter)
            .Select(entry => entry.Path)
            .ToArray();
    }

    /// <summary>Deletes every backup beyond the newest <see cref="Keep"/>.</summary>
    public void Prune()
    {
        foreach (var path in NewestFirst().Skip(Keep))
        {
            File.Delete(path);
        }
    }

    private static (string Stamp, int Counter) AgeKey(string fileName)
    {
        var rest = fileName[Prefix.Length..];
        var parts = rest.Split('-');
        if (parts.Length < 2)
        {
            return (rest, 1);
        }

        var stamp = parts[0] + "-" + parts[1];
        int counter = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 1;
        return (stamp, counter);
    }
}
