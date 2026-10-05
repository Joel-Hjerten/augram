namespace Augram.Core.Gestures;

/// <summary>
/// One raw cursor position of a stroke or a training sample, in whatever coordinate
/// space the capture produced (screen pixels today). Recognition is scale- and
/// position-invariant, so the space does not matter as long as X and Y share a unit.
/// </summary>
public readonly record struct GesturePoint(double X, double Y);
