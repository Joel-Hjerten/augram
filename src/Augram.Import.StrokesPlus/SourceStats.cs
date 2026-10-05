namespace Augram.Import.StrokesPlus;

/// <summary>
/// Counts of what the source document contains, including parts not imported yet, so the UI can
/// say "found 90 gestures, 212 actions (actions import comes later)". A null count means the
/// corresponding member was absent.
/// </summary>
public sealed record SourceStats(int GestureCount, int SampleCount, int? ActionCount, int? ApplicationCount);
