using System.Diagnostics;
using Augram.Core.Capture;
using Augram.Core.Config;

namespace Augram.App.Overlay;

/// <summary>
/// The hand-off between the engine worker and the UI thread (ADR-0002 §5a: marshalling lives here, not
/// in Core). The worker appends under a lock and asks for a render at most once until the UI thread
/// has taken the pending frame, so a burst of moves between two UI turns is one request and one draw.
/// <see cref="End"/> clears the points at once; the frame that follows draws nothing. No Avalonia types,
/// so it is tested without a window.
/// </summary>
public sealed class TrailBuffer
{
    private readonly Action _requestRender;
    private readonly object _gate = new();
    private readonly List<CapturePoint> _points = new(1024);
    private TrailSettings _style = TrailSettings.Default;
    private long _startedTicks;
    private bool _requestPending;
    private bool _strokeStart;
    private bool _active;
    private int _requests;

    /// <param name="requestRender">Called outside the lock, on the caller's thread, when a render is needed and none is pending.</param>
    public TrailBuffer(Action requestRender)
    {
        ArgumentNullException.ThrowIfNull(requestRender);
        _requestRender = requestRender;
    }

    /// <summary>How many render requests were issued; one per taken frame at most.</summary>
    public int RequestCount => Volatile.Read(ref _requests);

    /// <summary>True between <see cref="Begin"/> and <see cref="End"/>; the overlay watchdog reads it from the UI thread.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    public int PendingPointCount
    {
        get
        {
            lock (_gate)
            {
                return _points.Count;
            }
        }
    }

    /// <summary>Worker thread.</summary>
    public void Begin(CapturePoint start, TrailSettings style)
    {
        ArgumentNullException.ThrowIfNull(style);
        lock (_gate)
        {
            _points.Clear();
            _points.Add(start);
            _style = style;
            _startedTicks = Stopwatch.GetTimestamp();
            _strokeStart = true;
            _active = true;
        }

        Request();
    }

    /// <summary>Worker thread. Ignored outside a stroke.</summary>
    public void Extend(CapturePoint point)
    {
        lock (_gate)
        {
            if (!_active)
            {
                return;
            }

            _points.Add(point);
        }

        Request();
    }

    /// <summary>Worker thread.</summary>
    public void End()
    {
        lock (_gate)
        {
            _active = false;
            _points.Clear();
        }

        Request();
    }

    /// <summary>UI thread: the pending frame; a new request may be issued after this returns.</summary>
    public TrailFrame Take()
    {
        lock (_gate)
        {
            var frame = new TrailFrame([.. _points], _style, _strokeStart, _startedTicks, _active);
            _strokeStart = false;
            _requestPending = false;
            return frame;
        }
    }

    private void Request()
    {
        lock (_gate)
        {
            if (_requestPending)
            {
                return;
            }

            _requestPending = true;
            _requests++;
        }

        _requestRender();
    }
}
