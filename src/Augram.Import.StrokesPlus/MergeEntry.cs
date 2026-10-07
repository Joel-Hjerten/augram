using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// One imported gesture and what merging it means. <paramref name="Existing"/> is set for a
/// <see cref="MergeKind.Conflict"/> (same name) and a <see cref="MergeKind.SameShape"/> (the gesture it
/// scored against; <paramref name="ShapeScore"/> is that score), null for an <see cref="MergeKind.Add"/>.
/// </summary>
public sealed record MergeEntry(Gesture Imported, MergeKind Kind, Gesture? Existing, double? ShapeScore = null);
