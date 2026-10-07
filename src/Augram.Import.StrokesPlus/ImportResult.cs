using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Imported;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What an import produced: Core records with fresh ids, the report of what was skipped, dropped or
/// renamed, the counts found in the source, and the mapping (app groups, commands, ignored apps) whose
/// gesture triggers reference <see cref="Gestures"/> by id. A gestures-only import carries the empty mapping.
/// </summary>
public sealed record ImportResult(
    IReadOnlyList<Gesture> Gestures,
    IReadOnlyList<ImportWarning> Warnings,
    SourceStats Stats,
    MappingDocument Mapping)
{
    /// <summary>A gestures-only result (<see cref="StrokesPlusImporter.ReadGestures(StrokesPlusDocument)"/>): the mapping is <see cref="MappingDocument.Empty"/>.</summary>
    public ImportResult(IReadOnlyList<Gesture> gestures, IReadOnlyList<ImportWarning> warnings, SourceStats stats)
        : this(gestures, warnings, stats, MappingDocument.Empty)
    {
    }

    /// <summary>App groups other than Global.</summary>
    public int AppGroupCount => Mapping.Groups.Count(group => !group.IsGlobal);

    public int CommandCount => Mapping.AllCommands().Count();

    /// <summary>Steps imported as <see cref="ImportedStep"/> placeholders, waiting for their step type.</summary>
    public int PlaceholderStepCount => Mapping.AllCommands().Sum(entry => entry.Command.Steps.Count(step => step.Step is ImportedStep));

    public int IgnoredAppCount => Mapping.Ignored.Count;
}
