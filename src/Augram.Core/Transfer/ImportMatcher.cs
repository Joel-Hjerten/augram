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
    private readonly Dictionary<GestureId, GestureId> _gestureIds = [];

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

    private Gesture[] Gestures(IReadOnlyList<Gesture> file, IReadOnlyList<Gesture> local, RecognitionOptions? shapes)
    {
        var localIds = local.Select(gesture => gesture.Id).ToHashSet();
        var claimed = file.Select(gesture => gesture.Id).Where(localIds.Contains).ToHashSet();
        var unknown = file.Where(gesture => !localIds.Contains(gesture.Id)).ToArray();
        foreach (var gesture in unknown)
        {
            if (local.FirstOrDefault(twin => !claimed.Contains(twin.Id) && GestureRules.NameComparer.Equals(twin.Name, gesture.Name)) is { } twin)
            {
                Claim(gesture, twin, new ImportMatch(ImportMatchKind.Name, gesture.Name), claimed);
            }
        }

        if (shapes is not null)
        {
            MatchShapes(unknown.Where(gesture => !_gestureIds.ContainsKey(gesture.Id)), local, claimed, shapes);
        }

        return [.. file.Select(gesture => _gestureIds.TryGetValue(gesture.Id, out var id) ? gesture with { Id = id } : gesture)];
    }

    /// <summary>A7: the first sample against every gesture not yet matched (inactive ones too), a duplicate score taking its id.</summary>
    private void MatchShapes(IEnumerable<Gesture> unknown, IReadOnlyList<Gesture> local, HashSet<GestureId> claimed, RecognitionOptions shapes)
    {
        var matcher = new GestureMatcher();
        foreach (var gesture in unknown.Where(gesture => gesture.Samples.Count > 0))
        {
            var candidates = local.Where(twin => !claimed.Contains(twin.Id)).Select(twin => twin.IsActive ? twin : twin with { IsActive = true }).ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            var best = matcher.Rank(gesture.Samples[0], candidates, shapes).FirstOrDefault();
            if (best is not null && best.Score >= ConfusionCheck.DuplicateCutOff)
            {
                var twin = local.First(candidate => candidate.Id == best.GestureId);
                Claim(gesture, twin, new ImportMatch(ImportMatchKind.Shape, gesture.Name, best.Score), claimed);
            }
        }
    }

    private void Claim(Gesture gesture, Gesture twin, ImportMatch match, HashSet<GestureId> claimed)
    {
        claimed.Add(twin.Id);
        _gestureIds[gesture.Id] = twin.Id;
        _matches[SyncItemKey.ForGesture(twin.Id)] = match;
    }

    /// <summary>The file with its ids lined up, and how each item that took a local id by something other than its id matched, by its (local) key.</summary>
    internal sealed record Matched(IReadOnlyList<Gesture> Gestures, MappingDocument? Mapping, IReadOnlyDictionary<SyncItemKey, ImportMatch> Matches);
}
