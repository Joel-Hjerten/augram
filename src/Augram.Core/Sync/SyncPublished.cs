using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// One revision of this machine's own file as it was published: its items' contents and its <c>merged</c>
/// entries. Kept by <see cref="SyncBaseStore"/> (the newest <see cref="SyncBaseStore.KeepPublished"/>) because
/// another machine's acknowledgement names one of these revisions, and its items are then the base the two
/// machines share (README: base state).
/// </summary>
public sealed record SyncPublished(
    Guid Revision,
    DateTimeOffset WrittenAt,
    IReadOnlyDictionary<SyncItemKey, string> Items,
    IReadOnlyList<SyncAcknowledgement> Merged);
