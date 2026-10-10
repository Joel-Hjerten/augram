using Augram.Core.Recognition;

namespace Augram.Core.Transfer;

/// <summary>How <see cref="ImportPlan.Create"/> reads a file against the current configuration (plan 0003).</summary>
public sealed record ImportOptions
{
    public const string DefaultSourceName = "the file";

    public static ImportOptions Default { get; } = new();

    /// <summary>
    /// What the conflict rows and notes call the file's side, in the place of the sync's other machine ("Gesture · with
    /// Blender.augram.json", "… took the file's version"). The App passes the file's name.
    /// </summary>
    public string SourceName { get; init; } = DefaultSourceName;

    /// <summary>
    /// When set, a file gesture that matches no gesture here by id or name is matched by shape: its first sample scoring at or
    /// above <see cref="ConfusionCheck.DuplicateCutOff"/> against one (A7, as the StrokesPlus.net import does). The App passes
    /// the current recognition options; null (the default) matches by id and name only.
    /// </summary>
    public RecognitionOptions? MatchShapes { get; init; }
}
