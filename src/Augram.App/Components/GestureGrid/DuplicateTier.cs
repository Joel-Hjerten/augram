namespace Augram.App.Components.GestureGrid;

/// <summary>
/// How a tile is outlined (A7, DECIDED 2026-10-07): <see cref="Exact"/> when another gesture scores
/// at or above <see cref="Core.Recognition.ConfusionCheck.ExactCutOff"/> against it (the same shape
/// under two names), <see cref="Close"/> when a pair only passes the confusion cut-off.
/// </summary>
public enum DuplicateTier
{
    None,
    Close,
    Exact,
}
