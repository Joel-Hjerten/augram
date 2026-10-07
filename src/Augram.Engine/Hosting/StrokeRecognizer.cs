using System.Diagnostics;
using System.Globalization;
using System.Text;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Recognition;

namespace Augram.Engine.Hosting;

/// <summary>
/// The worker's button-up step (CLAUDE.md invariant 4): rank the stroke, write the Info line, return
/// the event for the App. A no-match entry is final here and goes into the recognition log at once;
/// a recognised gesture's entry is returned as a draft (<see cref="RecognitionResult.Draft"/>) for
/// whoever decides what fires to complete and add, so the panel shows the group and command next to
/// the match. Gestures and options come from delegates so the App can wire its stores without the
/// engine knowing them.
/// </summary>
internal sealed class StrokeRecognizer
{
    public const string NoGesturesReason = "no active gestures";

    private readonly GestureMatcher _matcher = new();
    private readonly Func<IReadOnlyList<Gesture>> _gestures;
    private readonly Func<RecognitionOptions> _options;
    private readonly RecognitionLog _recognitionLog;
    private readonly IEventLog _log;
    private readonly IClock _clock;

    public StrokeRecognizer(Func<IReadOnlyList<Gesture>> gestures, Func<RecognitionOptions> options, RecognitionLog recognitionLog, IEventLog log, IClock clock)
    {
        _gestures = gestures;
        _options = options;
        _recognitionLog = recognitionLog;
        _log = log;
        _clock = clock;
    }

    public RecognitionResult Recognize(CaptureOutcome.StrokeComplete stroke, double worstHandlerUs)
    {
        var options = _options();
        var points = new GesturePoint[stroke.Points.Count];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new GesturePoint(stroke.Points[i].X, stroke.Points[i].Y);
        }

        var started = Stopwatch.GetTimestamp();
        var ranked = _matcher.Rank(points, _gestures(), options);
        var matchMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var top = new RecognitionCandidate[Math.Min(ranked.Count, RecognitionLogEntry.MaxTopMatches)];
        for (var i = 0; i < top.Length; i++)
        {
            top[i] = new RecognitionCandidate(ranked[i].Name, ranked[i].Score, ranked[i].GestureId);
        }

        var best = ranked.Count > 0 && ranked[0].Score > options.Threshold && ranked[0].Score > 0 ? ranked[0] : null;
        var reason = best is not null
            ? null
            : ranked.Count == 0
                ? NoGesturesReason
                : string.Create(CultureInfo.InvariantCulture, $"best score {ranked[0].Score:F0} is not above threshold {options.Threshold:F0}");
        var durationMs = (int)(stroke.Points[^1].TimestampMs - stroke.Points[0].TimestampMs);

        var entry = new RecognitionLogEntry(_clock.UtcNow, points.Length, durationMs, top, NothingFiredReason: reason, MatchedGesture: best?.GestureId, Stroke: points);
        if (best is null)
        {
            _recognitionLog.Add(entry);
        }

        _log.Info(
            LogSources.Recognition,
            best is null ? "No match" : "Gesture recognized",
            ("gesture", best?.Name),
            ("score", best is null ? null : Math.Round(best.Score, 1)),
            ("points", points.Length),
            ("durationMs", durationMs),
            ("matchMs", Math.Round(matchMs, 2)),
            ("worstHandlerUs", Math.Round(worstHandlerUs, 1)),
            ("top", FormatTop(top)),
            ("reason", reason));

        return best is null
            ? new RecognitionResult(new EngineEvent.NoMatch(reason!, stroke.Start, stroke.Points, top), null)
            : new RecognitionResult(new EngineEvent.GestureRecognized(best.GestureId, best.Name, best.Score, stroke.Start, stroke.Points, top), entry);
    }

    /// <summary><c>name=score;name=score</c>, invariant, for grepping the file log.</summary>
    private static string FormatTop(IReadOnlyList<RecognitionCandidate> top)
    {
        var builder = new StringBuilder();
        foreach (var candidate in top)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(candidate.Name).Append('=').Append(candidate.Score.ToString("F0", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
