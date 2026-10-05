using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What an import produced: Core records with fresh ids, the report of what was skipped,
/// dropped or renamed, and the counts found in the source.
/// </summary>
public sealed record ImportResult(
    IReadOnlyList<Gesture> Gestures,
    IReadOnlyList<ImportWarning> Warnings,
    SourceStats Stats);
