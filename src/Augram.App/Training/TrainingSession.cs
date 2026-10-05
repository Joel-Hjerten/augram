using System.Globalization;
using Augram.App.Components.GestureDrawArea;
using Augram.Core.Gestures;
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
    private ScreenArea? _canvas;
    private bool _isOpen;

    public TrainingSession(GestureLibrary library, Func<RecognitionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(options);
        _library = library;
        _options = options;
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
        var ranked = Stroke.Count >= 2 ? _matcher.Rank(Stroke, _library.All, _options()) : [];
        BestMatch = ranked.Count > 0 ? ranked[0] : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryConsume(IReadOnlyList<GesturePoint> points, int startX, int startY)
    {
        ArgumentNullException.ThrowIfNull(points);
        var canvas = Volatile.Read(ref _canvas);
        if (!IsOpen || canvas is null || !canvas.Contains(startX, startY))
        {
            return false;
        }

        var local = points.Select(canvas.ToLocal).ToArray();
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

    /// <summary>Stores the stroke through the library's rules and ends the session.</summary>
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

        var sample = new GestureSample(Stroke);
        var stored = Target is { } target
            ? _library.Update(target with { Name = Name, Samples = [.. target.Samples, sample] })
            : _library.Add(new Gesture(GestureId.New(), Name, IsActive: true, [sample]));
        End(TrainingOutcome.Accepted);
        return stored;
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
