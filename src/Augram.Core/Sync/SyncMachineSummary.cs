namespace Augram.Core.Sync;

/// <summary>Another machine's file as the join question shows it: "PC-WORK, 105 gestures, 212 commands, synced 2026-10-07 09:12".</summary>
public sealed record SyncMachineSummary(Guid MachineId, string MachineName, DateTimeOffset WrittenAt, int Gestures, int Groups, int Commands);
