using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// The result of <see cref="GestureMerge.ApplyWithMap"/>: the merged library and, for every imported
/// gesture, the id it ended up under (<see cref="IdMap"/>: imported id → final id). An added or
/// kept-both gesture keeps its own id; a KeepMine or TakeTheirs resolves to the existing gesture's id,
/// which is what <see cref="MappingImport.Rebind"/> needs to point the imported commands at the right gesture.
/// </summary>
public sealed record MergeOutcome(IReadOnlyList<Gesture> Gestures, IReadOnlyDictionary<GestureId, GestureId> IdMap);
