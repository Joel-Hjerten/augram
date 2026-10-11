namespace Augram.Core.Sync;

/// <summary>The user's answer for one pending <see cref="SyncConflict"/>, as the conflict dialog returns it and <see cref="SyncCoordinator.Run"/> applies it before the run.</summary>
public sealed record SyncResolution(SyncConflict Conflict, SyncChoice Choice);
