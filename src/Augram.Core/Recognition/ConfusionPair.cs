using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>Two active gestures whose templates score each other above the threshold (checklist A7). <paramref name="Score"/> is the higher of the two directions.</summary>
public sealed record ConfusionPair(GestureId FirstId, string FirstName, GestureId SecondId, string SecondName, double Score);
