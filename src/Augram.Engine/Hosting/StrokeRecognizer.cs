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
/// The worker's button-up step (CLAUDE.md invariant 4): rank the stroke, write the recognition log
/// entry and the Info line (same facts, so the panel and the file agree), return the event for the
/// App. Gestures and options come from delegates so the App can wire its stores without the engine
/// knowing them. Nothing executes here in M1; the "nothing fired" reason says so.
/// </summary>
internal sealed class StrokeRecognizer
{
    public const string NoCommandReason = "no command mapped (M1)";
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

    public EngineEvent Recognize(CaptureOutcome.StrokeComplete stroke, double worstHandlerUs)
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
            top[i] = new RecognitionCandidate(ranked[i].Name, ranked[i].Score);
        }

        var best = ranked.Count > 0 && ranked[0].Score > options.Threshold && ranked[0].Score > 0 ? ranked[0] : null;
        var reason = best is not null
            ? NoCommandReason
            : ranked.Count == 0
                ? NoGesturesReason
                : string.Create(CultureInfo.InvariantCulture, $"best score {ranked[0].Score:F0} is not above threshold {options.Threshold:F0}");
        var durationMs = (int)(stroke.Points[^1].TimestampMs - stroke.Points[0].TimestampMs);

        _recognitionLog.Add(new RecognitionLogEntry(_clock.UtcNow, points.Length, durationMs, top, NothingFiredReason: reason));
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
            ? new EngineEvent.NoMatch(reason, stroke.Start, stroke.Points, top)
            : new EngineEvent.GestureRecognized(best.GestureId, best.Name, best.Score, stroke.Start, stroke.Points, top);
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
