using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// What this machine remembers about one other machine (README: base state), kept by <see cref="SyncBaseStore"/>.
/// <see cref="Base"/> is the merge base with it: per item, the content both machines last shared (a conflicted
/// item keeps its old entry until resolved). <see cref="MergedRevision"/> is the revision of its file last merged
/// here and <see cref="AppliedAcknowledgement"/> the revision of this machine's file whose acknowledgement (its
/// file's <c>merged</c> entry for us) was last folded into <see cref="Base"/>. <see cref="Held"/> are the keys it
/// said it has a pending conflict on with us; <see cref="Conflicts"/> are ours with it, waiting for the user.
/// </summary>
public sealed record SyncMachineState(Guid MachineId, string MachineName)
{
    public IReadOnlyDictionary<SyncItemKey, string> Base { get; init; } = new Dictionary<SyncItemKey, string>();

    public Guid? MergedRevision { get; init; }

    public Guid? AppliedAcknowledgement { get; init; }

    public IReadOnlyList<SyncItemKey> Held { get; init; } = [];

    public IReadOnlyList<SyncConflict> Conflicts { get; init; } = [];

    public DateTimeOffset? LastMerged { get; init; }

    /// <summary>
    /// The entry this machine's file carries for that machine: the revision merged, the keys not taken from it
    /// (our pending conflicts and the ones it holds) and the keys it should leave alone (our pending conflicts).
    /// Null before the first merge.
    /// </summary>
    public SyncAcknowledgement? Acknowledgement()
    {
        if (MergedRevision is not { } revision)
        {
            return null;
        }

        var pending = Conflicts.Select(conflict => conflict.Key).ToArray();
        var except = pending.Concat(Held).Distinct().Select(key => key.ToString()).ToArray();
        return new SyncAcknowledgement(MachineId, revision, except, pending.Select(key => key.ToString()).ToArray());
    }
}
