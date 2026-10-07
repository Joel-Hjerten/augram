using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// The store-thread half of <see cref="SyncCoordinator"/> (README: threading). The worker computes the next gestures
/// and mapping from a snapshot of the stores; the function handed to <c>onStoreThread</c> applies them only when the
/// stores still hold that snapshot (reference equality: every store change makes new instances), so an edit the
/// user made meanwhile is never overwritten; otherwise the worker computes again, at most
/// <see cref="SyncCoordinator.MaxAttempts"/> times. Applying is one <c>ReplaceAll</c> per store that changed.
/// </summary>
internal sealed class SyncStoreWriter
{
    private readonly GestureLibrary _gestures;
    private readonly MappingStore _mapping;
    private readonly Func<Func<SyncApplied>, SyncApplied> _onStoreThread;

    public SyncStoreWriter(GestureLibrary gestures, MappingStore mapping, Func<Func<SyncApplied>, SyncApplied> onStoreThread)
    {
        _gestures = gestures;
        _mapping = mapping;
        _onStoreThread = onStoreThread;
    }

    /// <summary>Computes from a snapshot (on the calling thread) and applies on the store thread when anything changed. Returns an error line, or null.</summary>
    public string? ComputeAndApply(Func<IReadOnlyList<Gesture>, MappingDocument, (IReadOnlyList<Gesture> Gestures, MappingDocument Mapping, SyncCounts Counts)> compute)
    {
        for (int attempt = 1; ; attempt++)
        {
            var gestures = _gestures.All;
            var mapping = _mapping.Current;
            var next = compute(gestures, mapping);
            if (next.Counts.IsEmpty)
            {
                return null;
            }

            var applied = _onStoreThread(() => Apply(gestures, mapping, next.Gestures, next.Mapping, next.Counts));
            if (applied.Error is { } error)
            {
                return error;
            }

            if (!applied.StoresMoved)
            {
                return null;
            }

            if (attempt == SyncCoordinator.MaxAttempts)
            {
                return "The gestures or commands kept changing during the sync; it will try again.";
            }
        }
    }

    /// <summary>Runs on the store thread: refuse a stale snapshot, validate both results, then replace what changed.</summary>
    private SyncApplied Apply(IReadOnlyList<Gesture> gesturesSeen, MappingDocument mappingSeen, IReadOnlyList<Gesture> gestures, MappingDocument mapping, SyncCounts counts)
    {
        if (!ReferenceEquals(_gestures.All, gesturesSeen) || !ReferenceEquals(_mapping.Current, mappingSeen))
        {
            return SyncApplied.Moved;
        }

        try
        {
            var validGestures = GestureRules.ValidSet(gestures);
            var validMapping = MappingRules.ValidDocument(mapping);
            if (counts.Gestures.Total > 0)
            {
                _gestures.ReplaceAll(validGestures);
            }

            if (counts.MappingChanged)
            {
                _mapping.ReplaceAll(validMapping);
            }

            return SyncApplied.Done;
        }
        catch (Exception ex) when (ex is GestureValidationException or MappingValidationException)
        {
            return SyncApplied.Failed($"The merged result was refused: {ex.Message}");
        }
    }
}
