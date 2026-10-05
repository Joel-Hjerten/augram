using System.Globalization;
using Augram.App.Declarations;
using Augram.Core.Diagnostics;

namespace Augram.App.ViewModels;

/// <summary>The Diagnostics › Recognition panel (A15) over <see cref="RecognitionLog"/>: one row per stroke, newest last.</summary>
public sealed class RecognitionViewModel
{
    public RecognitionViewModel(RecognitionLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
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

    public static string Result(RecognitionLogEntry e)
    {
        var group = e.MatchedGroup is null ? string.Empty : "[" + e.MatchedGroup + "] ";
        return e.FiredCommand is not null ? group + "fired " + e.FiredCommand : group + (e.NothingFiredReason ?? "nothing fired");
    }
}
