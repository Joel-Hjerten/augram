namespace Augram.Import.StrokesPlus;

/// <summary>What a <see cref="MergeEntry"/> will do to the library.</summary>
public enum MergeKind
{
    /// <summary>No existing gesture has this name or shape; it is added as is.</summary>
    Add,

    /// <summary>An existing gesture has this name (case-insensitive); the user picks a <see cref="MergeChoice"/>.</summary>
    Conflict,

    /// <summary>
    /// No existing gesture has this name, but one scores at or above
    /// <see cref="Core.Recognition.ConfusionCheck.DuplicateCutOff"/> against it (A7: a re-import reuses the
    /// existing gesture instead of adding a duplicate); the user picks a <see cref="MergeChoice"/>.
    /// </summary>
    SameShape,
}
