namespace Augram.Core.Diagnostics;

/// <summary>One row of the "top matches" column in the recognition log (A15): gesture name and 0–100 score.</summary>
public readonly record struct RecognitionCandidate(string Name, double Score);
