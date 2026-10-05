namespace Augram.Core.Gestures;

/// <summary>
/// A named gesture and the training samples that define it. Inactive gestures are kept
/// in the library but never recognised. Immutable: a change is a new record (ADR-0002 §6).
/// </summary>
public sealed record Gesture(GestureId Id, string Name, bool IsActive, IReadOnlyList<GestureSample> Samples);
