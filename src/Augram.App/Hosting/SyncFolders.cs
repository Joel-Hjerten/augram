using System.Globalization;
using Augram.Core.Abstractions;
using Augram.Core.Sync;

namespace Augram.App.Hosting;

/// <summary>
/// The sync folders under the config folder (F8 sync): <c>sync/repo</c> (the git clone), <c>sync/state</c>
/// (<see cref="SyncBaseStore"/>) and <c>sync/repository-url.txt</c>, the URL the clone and the state belong to.
/// Before every run the sync worker calls <see cref="Prepare"/> with the URL in the settings: when it differs
/// from the remembered one (changed or cleared), the old repository's state is cleared, so the next run asks the
/// join question again, and the old clone is moved aside to <c>sync/repo-old-&lt;timestamp&gt;</c>, never deleted
/// (the git adapter refuses a clone of another URL). The marker makes this hold across restarts: a URL changed
/// just before quitting is reset on the next start. Used from the sync worker only.
/// </summary>
public sealed class SyncFolders
{
    public const string FolderName = "sync";
    public const string RepositoryFolderName = "repo";
    public const string OldRepositoryPrefix = "repo-old-";
    public const string MarkerFileName = "repository-url.txt";

    private readonly IClock _clock;

    public SyncFolders(string configFolder, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFolder);
        ArgumentNullException.ThrowIfNull(clock);
        ConfigFolder = configFolder;
        Root = Path.Combine(configFolder, FolderName);
        _clock = clock;
    }

    public string ConfigFolder { get; }

    /// <summary><c>&lt;config&gt;/sync</c>.</summary>
    public string Root { get; }

    /// <summary>The clone's folder, handed to the git adapter.</summary>
    public string RepositoryFolder => Path.Combine(Root, RepositoryFolderName);

    public string MarkerPath => Path.Combine(Root, MarkerFileName);

    /// <summary>The URL the clone and the state belong to, or null when none is remembered.</summary>
    public string? RememberedUrl => File.Exists(MarkerPath) ? File.ReadAllText(MarkerPath).Trim() is { Length: > 0 } url ? url : null : null;

    /// <summary>
    /// Makes the folders belong to <paramref name="url"/> (null: sync off). Returns what happened: nothing, a
    /// first-time remember, or a reset (the clone moved to <see cref="SyncFolderReset.MovedTo"/>, the state cleared).
    /// </summary>
    /// <exception cref="IOException">The old clone could not be moved aside (a file in it is open); nothing was changed.</exception>
    /// <exception cref="UnauthorizedAccessException">The sync folder is not writable; nothing was changed.</exception>
    public SyncFolderReset Prepare(string? url, SyncBaseStore bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        var wanted = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        var remembered = RememberedUrl;
        if (string.Equals(remembered, wanted, StringComparison.Ordinal))
        {
            return SyncFolderReset.None;
        }

        if (remembered is null && wanted is not null)
        {
            // First sync with this marker: whatever is there (nothing, or a clone from before the marker existed) is this URL's.
            Remember(wanted);
            return SyncFolderReset.None;
        }

        // Move first: if the clone cannot move, nothing else changes and the next run tries again.
        var movedTo = MoveCloneAside();
        bases.Clear();
        if (wanted is null)
        {
            File.Delete(MarkerPath);
        }
        else
        {
            Remember(wanted);
        }

        return new SyncFolderReset(true, movedTo);
    }

    private void Remember(string url)
    {
        Directory.CreateDirectory(Root);
        var temp = MarkerPath + ".tmp";
        File.WriteAllText(temp, url);
        File.Move(temp, MarkerPath, overwrite: true);
    }

    private string? MoveCloneAside()
    {
        // An absent or empty folder holds nothing to keep, and git clones into either.
        var clone = RepositoryFolder;
        if (!Directory.Exists(clone) || !Directory.EnumerateFileSystemEntries(clone).Any())
        {
            return null;
        }

        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(Root, OldRepositoryPrefix + stamp);
        for (int n = 2; Directory.Exists(target) || File.Exists(target); n++)
        {
            target = Path.Combine(Root, $"{OldRepositoryPrefix}{stamp}-{n.ToString(CultureInfo.InvariantCulture)}");
        }

        Directory.Move(clone, target);
        return target;
    }
}
