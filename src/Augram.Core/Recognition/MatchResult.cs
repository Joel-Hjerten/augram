using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>Score of one gesture against one stroke, 0–100 under the active <see cref="ScoringMode"/>.</summary>
public sealed record MatchResult(GestureId GestureId, string Name, double Score);
