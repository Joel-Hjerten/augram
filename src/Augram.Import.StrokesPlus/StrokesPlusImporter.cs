using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Entry point of the importer. Composes one reader per SP.net section over a
/// <see cref="StrokesPlusDocument"/>: <see cref="ReadGestures(StrokesPlusDocument)"/> reads the gesture
/// library only (M1), <see cref="ReadAll(StrokesPlusDocument)"/> adds the applications, their actions
/// and the ignored applications as a <see cref="Core.Mapping.MappingDocument"/> whose gesture triggers
/// point at the ids of the gestures read from the same file (M2 step 8, plan 0001 §C1).
/// </summary>
public static class StrokesPlusImporter
{
    /// <summary>Imports the gestures of a StrokesPlus.net JSON text (BOM tolerated).</summary>
    /// <exception cref="ImportFormatException">The text is not a JSON object.</exception>
    public static ImportResult ReadGestures(string json)
    {
        using var document = StrokesPlusDocument.Parse(json);
        return ReadGestures(document);
    }

    /// <summary>Imports the gestures of a StrokesPlus.net JSON stream (BOM tolerated).</summary>
    /// <exception cref="ImportFormatException">The stream is not a JSON object.</exception>
    public static ImportResult ReadGestures(Stream stream)
    {
        using var document = StrokesPlusDocument.Parse(stream);
        return ReadGestures(document);
    }

    public static ImportResult ReadGestures(StrokesPlusDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<ImportWarning>();
        var gestures = new GestureReader(warnings).Read(document.Root);
        var stats = SourceStatsReader.Read(document.Root);
        return new ImportResult(gestures, warnings, stats);
    }

    /// <summary>Imports gestures, app groups, commands and ignored apps from a StrokesPlus.net JSON text (BOM tolerated).</summary>
    /// <exception cref="ImportFormatException">The text is not a JSON object.</exception>
    public static ImportResult ReadAll(string json)
    {
        using var document = StrokesPlusDocument.Parse(json);
        return ReadAll(document);
    }

    /// <summary>Imports gestures, app groups, commands and ignored apps from a StrokesPlus.net JSON stream (BOM tolerated).</summary>
    /// <exception cref="ImportFormatException">The stream is not a JSON object.</exception>
    public static ImportResult ReadAll(Stream stream)
    {
        using var document = StrokesPlusDocument.Parse(stream);
        return ReadAll(document);
    }

    public static ImportResult ReadAll(StrokesPlusDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<ImportWarning>();
        var gestures = new GestureReader(warnings).Read(document.Root);
        var steps = new StepReader(warnings);
        var actions = new ActionReader(warnings, ByName(gestures), steps);
        var groups = new ApplicationReader(warnings, actions).Read(document.Root);
        var ignored = new IgnoredApplicationReader(warnings).Read(document.Root);
        steps.ReportPlaceholders();
        var mapping = MappingAssembler.Assemble(groups, ignored, warnings);
        return new ImportResult(gestures, warnings, SourceStatsReader.Read(document.Root), mapping);
    }

    /// <summary>SP.net binds actions to gestures by name; the first gesture with a name wins, as it does there.</summary>
    private static IReadOnlyDictionary<string, GestureId> ByName(IReadOnlyList<Gesture> gestures)
    {
        var byName = new Dictionary<string, GestureId>(GestureRules.NameComparer);
        foreach (var gesture in gestures)
        {
            byName.TryAdd(gesture.Name, gesture.Id);
        }

        return byName;
    }
}
