namespace Augram.Import.StrokesPlus;

/// <summary>
/// Counts of what the source document contains, before any mapping, so the report can say what was
/// found next to what was imported: the gestures in the source, usable or not.
/// </summary>
public sealed record SourceStats(int GestureCount);
