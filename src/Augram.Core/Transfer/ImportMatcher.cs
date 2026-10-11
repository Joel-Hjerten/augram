using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// Lines a file up with this configuration before the merge (plan 0003, decision 5). Ids are kept: a file item whose id is
/// here is that item. A file item whose id is unknown here takes the id of the local item it matches (a gesture by name,
/// then by shape when asked; a group by name; within a matched group a category by name, a hold remap by hold key then
/// name, a command by name among its siblings; an ignored app by name), and every reference to it in the file follows (a
/// command's gesture, category, hold remap; a command's "Not in" and "Also in", which name ignored apps). A local item already matched by
/// id, or by an earlier file item, is never matched again, so no two file items end up with one key. The result then merges like a re-import of the same items.
/// Pure. The mapping half is in <c>ImportMatcher.Mapping.cs</c>.
/// </summary>
internal sealed partial class ImportMatcher
{
    private readonly Dictionary<SyncItemKey, ImportMatch> _matches = [];
    private Dictionary<GestureId, GestureId> _gestureIds = [];

    private ImportMatcher()
    {
    }

    /// <param name="file">The file as read.</param>
    /// <param name="gestures">The gestures here.</param>
    /// <param name="mapping">The mapping here, validated.</param>
    /// <param name="shapes">Recognition options to match gestures by shape with; null for id and name only.</param>
    public static Matched Match(TransferFile file, IReadOnlyList<Gesture> gestures, MappingDocument mapping, RecognitionOptions? shapes)
    {
        var matcher = new ImportMatcher();
        var matchedGestures = matcher.Gestures(file.Gestures, gestures, shapes);
        var matchedMapping = file.Mapping is { } fileMapping ? matcher.Mapping(fileMapping, mapping) : null;
        return new Matched(matchedGestures, matchedMapping, matcher._matches);
    }

    /// <summary>By id, then by name (<see cref="Pair"/>, as every other kind), then by shape when asked.</summary>
    private Gesture[] Gestures(IReadOnlyList<Gesture> file, IReadOnlyList<Gesture> local, RecognitionOptions? shapes)
    {
        _gestureIds = Pair(
            file,
            local,
            gesture => gesture.Id,
            gesture => gesture.Name,
            SyncItemKey.ForGesture,
            (ImportMatchKind.Name, (gesture, twin) => GestureRules.NameComparer.Equals(twin.Name, gesture.Name)));
        if (shapes is not null)
        {
            var localIds = local.Select(gesture => gesture.Id).ToHashSet();
            var claimed = file.Select(gesture => gesture.Id).Where(localIds.Contains).Concat(_gestureIds.Values).ToHashSet();
            MatchShapes(file.Where(gesture => !localIds.Contains(gesture.Id) && !_gestureIds.ContainsKey(gesture.Id)), local, claimed, shapes);
        }

        return [.. file.Select(gesture => _gestureIds.TryGetValue(gesture.Id, out var id) ? gesture with { Id = id } : gesture)];
    }

    /// <summary>A7: the first sample against every gesture not yet matched (inactive ones too), a duplicate score taking its id (<see cref="ConfusionCheck.Duplicate"/>).</summary>
    private void MatchShapes(IEnumerable<Gesture> unknown, IReadOnlyList<Gesture> local, HashSet<GestureId> claimed, RecognitionOptions shapes)
    {
        var matcher = new GestureMatcher();
        foreach (var gesture in unknown.Where(gesture => gesture.Samples.Count > 0))
        {
            var candidates = local.Where(twin => !claimed.Contains(twin.Id)).ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            if (ConfusionCheck.Duplicate(gesture.Samples[0], candidates, shapes, matcher) is { } best)
            {
                claimed.Add(best.GestureId);
                _gestureIds[gesture.Id] = best.GestureId;
                _matches[SyncItemKey.ForGesture(best.GestureId)] = new ImportMatch(ImportMatchKind.Shape, gesture.Name, best.Score);
            }
        }
    }

    /// <summary>The file with its ids lined up, and how each item that took a local id by something other than its id matched, by its (local) key.</summary>
    internal sealed record Matched(IReadOnlyList<Gesture> Gestures, MappingDocument? Mapping, IReadOnlyDictionary<SyncItemKey, ImportMatch> Matches);
}
