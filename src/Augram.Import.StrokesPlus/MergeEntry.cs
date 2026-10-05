using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>One imported gesture and what merging it means. <paramref name="Existing"/> is set only for conflicts.</summary>
public sealed record MergeEntry(Gesture Imported, MergeKind Kind, Gesture? Existing);
