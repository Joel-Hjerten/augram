using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Avalonia.Threading;

namespace Augram.App.Overlay;

/// <summary>
/// The trail overlay (F6) over a platform's own window, <see cref="ITrailSurface"/>, for where <see cref="TrailOverlayWindow"/>
/// cannot go: on macOS an Avalonia window never shows over another app's full-screen Space (2026-10-09), a native panel does.
/// Same rules as the window: nothing native exists until the first stroke; every stroke's first frame verifies the surface
/// (<see cref="OverlayStylePolicy"/>) before it covers the display under the stroke start, and a surface that is not
/// click-through is hidden, logged as an error, and the trail is a no-op for the rest of the run (CLAUDE.md invariant 6).
/// Between strokes the surface is parked at one point, not hidden, and its path cleared; a 1 s watchdog hides a surface still
/// full-size <see cref="TrailOverlayWindow.IdleVisibleLimit"/> after its stroke ended. Trail calls arrive on the engine worker
/// and go through <see cref="TrailBuffer"/>; the UI thread draws one coalesced frame per request. The first frame of two or
/// more points is timed from <c>Begin</c> to the moment it is handed to the surface, and reported to the log and the health summary.
/// The surface belongs to the service container, which disposes it.
/// </summary>
public sealed class NativeTrailOverlay : IStrokeTrail, IDisposable
{
    public const string LogSource = TrailOverlayWindow.LogSource;
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(1);

    private readonly Func<TrailSettings> _trail;
    private readonly ITrailSurface _surface;
    private readonly IEventLog _log;
    private readonly IDisposable? _healthRegistration;
    private readonly TrailBuffer _buffer;
    private readonly DispatcherTimer _watchdog;
    private double _lastFirstFrameMs = -1;
    private long _strokeStartedTicks;
    private bool _firstFramePending;
    private int _usable = 1;
    private SurfaceState _state;
    private DateTimeOffset _visibleSince;
    private TrailSurfaceArea _loggedArea;
    private string? _loggedStyle;

    public NativeTrailOverlay(Func<TrailSettings> trail, ITrailSurface surface, IEventLog log, HealthRegistry? health)
    {
        ArgumentNullException.ThrowIfNull(trail);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(log);
        _trail = trail;
        _surface = surface;
        _log = log;
        _buffer = new TrailBuffer(RequestRender);
        _watchdog = new DispatcherTimer(WatchdogInterval, DispatcherPriority.Background, (_, _) => CheckIdle(DateTimeOffset.UtcNow));
        _watchdog.Start();

        _healthRegistration = health?.Register(snapshot =>
        {
            var ms = Volatile.Read(ref _lastFirstFrameMs);
            return ms < 0 ? snapshot : snapshot with { OverlayFirstFrameMs = ms };
        });
    }

    private enum SurfaceState
    {
        Hidden,
        Parked,
        Shown,
    }

    /// <summary>False once the surface reported itself as not click-through; the trail is a no-op from then on.</summary>
    public bool IsUsable => Volatile.Read(ref _usable) != 0;

    /// <summary>Frames taken from the buffer since startup; one per render request.</summary>
    public int FramesRendered { get; private set; }

    /// <summary>Ordered in, full-size or parked.</summary>
    public bool IsVisible => _state != SurfaceState.Hidden;

    /// <summary>True while the surface is ordered in but shrunk to one point between strokes.</summary>
    public bool IsParked => _state == SurfaceState.Parked;

    internal TrailBuffer Buffer => _buffer;

    public void Begin(CapturePoint start)
    {
        if (IsUsable)
        {
            _buffer.Begin(start, _trail());
        }
    }

    public void Extend(CapturePoint point)
    {
        if (IsUsable)
        {
            _buffer.Extend(point);
        }
    }

    public void End()
    {
        if (IsUsable)
        {
            _buffer.End();
        }
    }

    public void Dispose()
    {
        _watchdog.Stop();
        _healthRegistration?.Dispose();
    }

    /// <summary>The watchdog's check, with the clock injected for tests: a surface still full-size <see cref="TrailOverlayWindow.IdleVisibleLimit"/> after the stroke ended is hidden and reported.</summary>
    internal void CheckIdle(DateTimeOffset now)
    {
        if (_state == SurfaceState.Shown && !_buffer.IsActive && now - _visibleSince > TrailOverlayWindow.IdleVisibleLimit)
        {
            HideNow();
            _log.Error(LogSource, "Overlay visible without a stroke, hidden", ("visibleForMs", (now - _visibleSince).TotalMilliseconds));
        }
    }

    /// <summary>The UI thread's turn: draws the pending frame.</summary>
    private void RenderFrame()
    {
        var frame = _buffer.Take();
        FramesRendered++;
        if (!IsUsable)
        {
            return;
        }

        if (!frame.IsActive)
        {
            HideIdle();
            return;
        }

        if (frame.IsStrokeStart && frame.Points.Count > 0)
        {
            StartStroke(frame);
            return;
        }

        if (_state == SurfaceState.Shown)
        {
            Draw(frame.Points);
        }
    }

    private void RequestRender() => Dispatcher.UIThread.Post(RenderFrame, DispatcherPriority.Render);

    /// <summary>Verify, style, cover the display, draw, then order in: the first full-size frame is already the new stroke.</summary>
    private void StartStroke(TrailFrame frame)
    {
        if (!VerifyOrHide())
        {
            return;
        }

        _strokeStartedTicks = frame.StrokeStartedTicks;
        _firstFramePending = true;
        // Shown before it grows: should a call below throw, the watchdog still sees a full-size surface and hides it.
        var wasHidden = _state == SurfaceState.Hidden;
        _state = SurfaceState.Shown;
        _visibleSince = DateTimeOffset.UtcNow;
        _surface.SetStyle(frame.Style);
        var area = _surface.Cover(frame.Points[0]);
        if (area != _loggedArea)
        {
            _loggedArea = area;
            _log.Info(LogSource, "Overlay area", ("bounds", area), ("surface", "native"));
        }

        Draw(frame.Points);
        if (wasHidden)
        {
            _surface.Show();
        }
    }

    private void Draw(IReadOnlyList<CapturePoint> points)
    {
        _surface.SetPoints(points);
        if (!_firstFramePending || points.Count < 2)
        {
            return;
        }

        _firstFramePending = false;
        var ms = (Stopwatch.GetTimestamp() - _strokeStartedTicks) * 1000.0 / Stopwatch.Frequency;
        Volatile.Write(ref _lastFirstFrameMs, ms);
        _log.Info(LogSource, "Trail first frame", ("firstFrameMs", Math.Round(ms, 2)), ("points", points.Count));
    }

    /// <summary>
    /// Between strokes the surface stays ordered in, cleared and shrunk to one point ("parked"), as the window does: hiding the
    /// Avalonia window left the previous stroke in its retained surface, which flashed on the next stroke (2026-10-06).
    /// </summary>
    private void HideIdle()
    {
        if (_state != SurfaceState.Shown)
        {
            return;
        }

        _surface.SetPoints([]);
        _surface.Park();
        _state = SurfaceState.Parked;
    }

    /// <summary>Safety path: hide now.</summary>
    private void HideNow()
    {
        _surface.SetPoints([]);
        _surface.Hide();
        _state = SurfaceState.Hidden;
    }

    /// <summary>Every stroke start: the surface may cover a display only when it reports itself click-through right now.</summary>
    private bool VerifyOrHide()
    {
        var report = _surface.Verify();
        if (OverlayStylePolicy.Decide(report) == OverlayStyleDecision.Show)
        {
            if (report.Raw != _loggedStyle)
            {
                _loggedStyle = report.Raw;
                if (OverlayStylePolicy.HasCosmeticFault(report))
                {
                    _log.Warning(LogSource, "Overlay style incomplete", ("style", report.Raw), ("noActivate", report.NoActivate), ("toolWindow", report.ToolWindow));
                }
                else
                {
                    _log.Info(LogSource, "Overlay style verified", ("style", report.Raw));
                }
            }

            return true;
        }

        Volatile.Write(ref _usable, 0);
        _buffer.End();
        HideNow();
        _log.Error(LogSource, "Overlay not click-through, hidden", ("style", report.Raw));
        return false;
    }
}
