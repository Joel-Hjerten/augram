using System.Globalization;
using Augram.App.Components.GestureDrawArea;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Gestures.Cleanup;
using Augram.Core.Recognition;
using Avalonia.Threading;

namespace Augram.App.Training;

/// <summary>
/// The training state (ADR-0002 §5a: a service, not a window): which gesture is being trained, the
/// current stroke (one; a redraw replaces it, F3), the live best match, and the save step. The window
/// is a thin view over this. Single stroke, no averaging. Strokes arrive either from the draw area
/// (left button, UI thread) or from the engine through <see cref="TryConsume"/> (stroke button,
/// worker thread), both ending in <see cref="ReplaceStroke"/> on the UI thread.
/// </summary>
public sealed class TrainingSession : ITrainingSession
{
    private readonly GestureLibrary _library;
    private readonly Func<RecognitionOptions> _options;
    private readonly GestureMatcher _matcher = new();
    private readonly IEventLog _log;
    private ScreenArea? _canvas;
    private bool _isOpen;

    public TrainingSession(GestureLibrary library, Func<RecognitionOptions> options, IEventLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(options);
        _library = library;
        _options = options;
        _log = log ?? NullEventLog.Instance;
    }

    public event EventHandler? Started;

    /// <summary>The stroke, the best match or the name changed.</summary>
    public event EventHandler? Changed;

    public event EventHandler<TrainingOutcome>? Ended;

    public bool IsOpen => Volatile.Read(ref _isOpen);

    public TrainingRequest? Request { get; private set; }

    /// <summary>The gesture a sample is being added to; null for a new gesture.</summary>
    public Gesture? Target => Request?.GestureId is { } id ? _library.Find(id) : null;

    public string Name
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    } = string.Empty;

    public IReadOnlyList<GesturePoint> Stroke { get; private set; } = [];

    /// <summary>The stroke's cleaned shape (plan 0001 M2 step 10), for the preview; empty without a stroke.</summary>
    public IReadOnlyList<GesturePoint> CleanedStroke { get; private set; } = [];

    /// <summary>
    /// Clean up shape (Joel, 2026-10-08: on by default): Accept stores the cleaned shape and keeps the stroke as its
    /// original, and the best match compares the cleaned shape. Kept for the rest of the run once changed.
    /// </summary>
    public bool CleanUp
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Rank();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    } = true;

    /// <summary>The best-scoring active gesture for the current stroke, whatever its score; null without a stroke.</summary>
    public MatchResult? BestMatch { get; private set; }

    public void Begin(TrainingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsOpen)
        {
            End(TrainingOutcome.Cancelled);
        }

        Request = request;
        Name = Target?.Name ?? NextName();
        Stroke = [];
        CleanedStroke = [];
        BestMatch = null;
        Volatile.Write(ref _isOpen, true);
        Started?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The draw area tells the session where it is on screen (null when hidden), so <see cref="TryConsume"/> can route by start point.</summary>
    public void PublishCanvas(ScreenArea? area) => Volatile.Write(ref _canvas, area);

    public void ReplaceStroke(IReadOnlyList<GesturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (!IsOpen)
        {
            return;
        }

        Stroke = points.ToArray();
        CleanedStroke = Stroke.Count >= 2 ? ShapeCleanup.Clean(Stroke).Points : [];
        Rank();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryConsume(IReadOnlyList<GesturePoint> points, int startX, int startY)
    {
        ArgumentNullException.ThrowIfNull(points);
        var canvas = Volatile.Read(ref _canvas);
        if (!IsOpen)
        {
            return false;
        }

        if (canvas is null || !canvas.Contains(startX, startY))
        {
            _log.Info("training", "Stroke outside the draw area", ("start", $"{startX},{startY}"), ("canvas", canvas is null ? "none" : $"{canvas.Left:0},{canvas.Top:0} {canvas.Width:0}x{canvas.Height:0}"));
            return false;
        }

        var local = points.Select(canvas.ToLocal).ToArray();
        // Where a stroke-button stroke landed in the draw area (Joel, 2026-10-09: it showed in the corner): enough to see an offset.
        _log.Info("training", "Stroke drawn into the draw area", ("start", $"{startX},{startY}"), ("canvas", $"{canvas.Left:0},{canvas.Top:0} {canvas.Width:0}x{canvas.Height:0} ×{canvas.Scale}"), ("firstLocal", local.Length > 0 ? $"{local[0].X:0},{local[0].Y:0}" : "none"));
        if (Dispatcher.UIThread.CheckAccess())
        {
            ReplaceStroke(local);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ReplaceStroke(local));
        }

        return true;
    }

    /// <summary>Stores the stroke through the library's rules and ends the session. A redraw replaces every earlier sample (F3: no averaging).</summary>
    /// <exception cref="GestureValidationException">No stroke yet, or the library rejects the name or sample; the message is for the user.</exception>
    public Gesture Accept()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("No training session is open.");
        }

        if (Stroke.Count == 0)
        {
            throw new GestureValidationException("Draw the gesture first.");
        }

        var stored = Target is { } target
            ? _library.Update(GestureCleanup.FromStroke(target with { Name = Name }, Stroke, CleanUp))
            : _library.Add(GestureCleanup.FromStroke(new Gesture(GestureId.New(), Name, IsActive: true, []), Stroke, CleanUp));
        End(TrainingOutcome.Accepted);
        return stored;
    }

    /// <summary>The best match for what Accept would store: the cleaned shape when Clean up shape is on.</summary>
    private void Rank()
    {
        var stored = CleanUp ? CleanedStroke : Stroke;
        var ranked = stored.Count >= 2 ? _matcher.Rank(stored, _library.All, _options()) : [];
        BestMatch = ranked.Count > 0 ? ranked[0] : null;
    }

    public void Cancel()
    {
        if (IsOpen)
        {
            End(TrainingOutcome.Cancelled);
        }
    }

    /// <summary>"New gesture N" with the smallest N the library does not have yet.</summary>
    public string NextName()
    {
        var taken = new HashSet<string>(_library.All.Select(gesture => gesture.Name), GestureRules.NameComparer);
        for (var n = 1; ; n++)
        {
            var candidate = "New gesture " + n.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private void End(TrainingOutcome outcome)
    {
        Volatile.Write(ref _isOpen, false);
        Ended?.Invoke(this, outcome);
    }
}
