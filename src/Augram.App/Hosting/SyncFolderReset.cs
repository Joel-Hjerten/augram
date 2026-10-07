namespace Augram.App.Hosting;

/// <summary>What <see cref="SyncFolders.Prepare"/> did: <see cref="Reset"/> when the old repository's state was cleared, and where its clone went (null when there was none).</summary>
public sealed record SyncFolderReset(bool Reset, string? MovedTo)
{
    public static SyncFolderReset None { get; } = new(false, null);
}
