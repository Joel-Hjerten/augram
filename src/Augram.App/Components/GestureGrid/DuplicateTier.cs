namespace Augram.App.Components.GestureGrid;

/// <summary>
/// How confusable a tile is (A7, DECIDED 2026-10-07): <see cref="Exact"/> when another gesture scores
/// at or above <see cref="Core.Recognition.ConfusionCheck.ExactCutOff"/> against it (the same shape
/// under two names; outlined), <see cref="Close"/> when a pair only passes the confusion cut-off
/// (not outlined: the recognizer calls a vertical down-up and a V close, a user would not; the
/// partner score on select is enough).
/// </summary>
public enum DuplicateTier
{
    None,
    Close,
    Exact,
}
