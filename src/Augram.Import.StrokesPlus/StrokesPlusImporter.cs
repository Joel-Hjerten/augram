namespace Augram.Import.StrokesPlus;

/// <summary>
/// Entry point of the importer. Composes one reader per SP.net section (gestures today;
/// actions and applications later, as separate reader files) over a <see cref="StrokesPlusDocument"/>.
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
}
