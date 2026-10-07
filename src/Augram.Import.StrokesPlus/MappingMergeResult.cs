using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What <see cref="MappingImport.Merge"/> produced: the merged, validated document and the counts
/// the dialog and the log report. A skipped command is one whose name, or whose bound trigger, was
/// already taken in its group; a skipped ignored app is one whose name already existed.
/// </summary>
public sealed record MappingMergeResult(
    MappingDocument Document,
    int GroupsAdded,
    int CommandsAdded,
    int CommandsSkipped,
    int IgnoredAdded,
    int IgnoredSkipped);
