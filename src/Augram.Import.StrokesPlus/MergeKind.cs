namespace Augram.Import.StrokesPlus;

/// <summary>What a <see cref="MergeEntry"/> will do to the library.</summary>
public enum MergeKind
{
    /// <summary>No existing gesture has this name; it is added as is.</summary>
    Add,

    /// <summary>An existing gesture has this name (case-insensitive); the user picks a <see cref="MergeChoice"/>.</summary>
    Conflict,
}
