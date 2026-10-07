using Augram.Core.Sync;

namespace Augram.App.Hosting;

/// <summary>The user's answer for one pending <see cref="SyncConflict"/>, as the conflict dialog returns it and <see cref="SyncService.Resolve"/> applies it.</summary>
public sealed record SyncResolution(SyncConflict Conflict, SyncChoice Choice);
