namespace Augram.Import.StrokesPlus;

/// <summary>
/// Counts of what the source document contains, before any mapping, so the report can say what was
/// found next to what was imported. A null count means the corresponding member was absent.
/// </summary>
public sealed record SourceStats(
    int GestureCount,
    int SampleCount,
    int? ActionCount,
    int? ApplicationCount,
    int? StepCount = null,
    int? IgnoredApplicationCount = null);
