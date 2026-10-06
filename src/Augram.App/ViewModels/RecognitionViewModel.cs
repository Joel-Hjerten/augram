using System.Globalization;
using Augram.App.Declarations;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;

namespace Augram.App.ViewModels;

/// <summary>
/// The Diagnostics › Recognition panel (A15) over <see cref="RecognitionLog"/>: one row per stroke,
/// newest last. The matched gesture's glyph comes from the <see cref="GestureLibrary"/> by id, so a
/// renamed or retrained gesture shows its current look; a deleted one shows an empty tile.
/// </summary>
public sealed class RecognitionViewModel
{
    private readonly GestureLibrary _library;

    public RecognitionViewModel(RecognitionLog log, GestureLibrary library)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(library);
        _library = library;
        Source = new ListSource<RecognitionLogEntry>(log.Snapshot);
        log.Changed += (_, _) => Source.NotifyChanged();
    }

    public ListSource<RecognitionLogEntry> Source { get; }

    public static string Time(RecognitionLogEntry e) => e.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public static string Points(RecognitionLogEntry e) => e.PointCount.ToString(CultureInfo.InvariantCulture);

    public static string Duration(RecognitionLogEntry e) => e.DurationMs.ToString(CultureInfo.InvariantCulture) + " ms";

    public static string TopMatches(RecognitionLogEntry e) =>
        e.TopMatches.Count == 0
            ? "–"
            : string.Join("  ·  ", e.TopMatches.Select(m => m.Name + " " + m.Score.ToString("0", CultureInfo.InvariantCulture)));

    /// <summary>The matched gesture's name and score, or an en dash when nothing passed the threshold.</summary>
    public static string Matched(RecognitionLogEntry e) =>
        e.MatchedGesture is null || e.TopMatches.Count == 0
            ? "–"
            : e.TopMatches[0].Name + " " + e.TopMatches[0].Score.ToString("0", CultureInfo.InvariantCulture);

    public static string Result(RecognitionLogEntry e)
    {
        var group = e.MatchedGroup is null ? string.Empty : "[" + e.MatchedGroup + "] ";
        return e.FiredCommand is not null ? group + "fired " + e.FiredCommand : group + (e.NothingFiredReason ?? "nothing fired");
    }

    /// <summary>The stroke as drawn, for the "Drawn" glyph.</summary>
    public static IReadOnlyList<GesturePoint>? DrawnPoints(RecognitionLogEntry e) => e.Stroke;

    /// <summary>The matched gesture's first sample, for the "Matched" glyph; null when nothing matched or the gesture is gone.</summary>
    public IReadOnlyList<GesturePoint>? MatchedPoints(RecognitionLogEntry e)
    {
        if (e.MatchedGesture is not { } id || _library.Find(id) is not { Samples.Count: > 0 } gesture)
        {
            return null;
        }

        return gesture.Samples[0];
    }
}
