using Augram.Core.Gestures;

namespace Augram.Core.Diagnostics;

/// <summary>
/// One row of the "top matches" column in the recognition log (A15): gesture name, 0–100 score, and
/// the gesture's id so a view can show the gesture's own glyph next to the score.
/// </summary>
public readonly record struct RecognitionCandidate(string Name, double Score, GestureId Id = default);
