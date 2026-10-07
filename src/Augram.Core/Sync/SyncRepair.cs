namespace Augram.Core.Sync;

/// <summary>One change a merge made so its result is valid, with a line fit for the Options page ("Incoming gesture 'Zig' renamed 'Zig (2)': the name is taken.").</summary>
public sealed record SyncRepair(SyncItemKey Key, SyncRepairKind Kind, string Description);
