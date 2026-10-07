namespace Augram.Core.Sync;

/// <summary>
/// The answer to <see cref="SyncStatus.NeedsJoinChoice"/>: what a machine does the first time it meets a repo
/// that already holds other machines' files (F8 sync).
/// </summary>
public enum SyncJoin
{
    /// <summary>"Use the synced settings on this machine" (the default): replace the local gestures and mapping with the newest other machine's, one undo step per store, after the config file's usual backup.</summary>
    UseRemote,

    /// <summary>Merge both sets as if nothing were shared: everything is kept, clashing names renamed. Two machines that imported StrokesPlus.net separately get everything twice.</summary>
    Merge,
}
