using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Augram.App.Overlay;

/// <summary>
/// The trail overlay (F6): one transparent, click-through, non-activating, topmost window that covers the
/// virtual screen only while a stroke is in progress. Hidden when idle; shown by the first frame of a
/// stroke after the native styles were applied and read back (<see cref="OverlayStylePolicy"/>); hidden
/// again by the stroke's last frame. If the OS ever reports the window as not click-through it is hidden
/// at once, logged as an error, and the trail becomes a no-op for the rest of the run: a full-screen
/// window that takes clicks blocks the user's mouse. A watchdog hides a window that is still visible
/// <see cref="IdleVisibleLimit"/> after the stroke ended. <see cref="IStrokeTrail"/> arrives on the engine
/// worker through <see cref="TrailBuffer"/>; the UI thread draws one coalesced frame per request. Pen width
/// is the setting scaled by the DPI of the monitor under the stroke start. The first rendered segment is
/// timed from <c>Begin</c> (Show included) and reported to the log and the health summary.
/// </summary>
public sealed class TrailOverlayWindow : Window, IStrokeTrail, IDisposable
{
    public const string LogSource = "overlay";
    public static readonly TimeSpan IdleVisibleLimit = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(1);
    private static readonly PixelRect FallbackBounds = new(0, 0, 1920, 1080);

    private readonly Func<TrailSettings> _trail;
    private readonly IOverlayWindowStyle _style;
    private readonly IEventLog _log;
    private readonly IDisposable? _healthRegistration;
    private readonly TrailCanvas _canvas = new();
    private readonly TrailBuffer _buffer;
    private readonly DispatcherTimer _watchdog;
    private double _lastFirstFrameMs = -1;
    private long _strokeStartedTicks;
    private bool _firstFramePending;
    private int _usable = 1;
    private DateTimeOffset _visibleSince;

    public TrailOverlayWindow(Func<TrailSettings> trail, IOverlayWindowStyle style, IEventLog log, HealthRegistry? health)
    {
        ArgumentNullException.ThrowIfNull(trail);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(log);
        _trail = trail;
        _style = style;
        _log = log;
        _buffer = new TrailBuffer(RequestRender);

        Title = "Augram trail";
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        CanResize = false;
        IsHitTestVisible = false;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = 1;
        Height = 1;
        Content = _canvas;
        _canvas.Rendered += OnRendered;
        Opened += (_, _) => VerifyOrHide("opened");
        _watchdog = new DispatcherTimer(WatchdogInterval, DispatcherPriority.Background, (_, _) => CheckIdle(DateTimeOffset.UtcNow));
        _watchdog.Start();

        _healthRegistration = health?.Register(snapshot =>
        {
            var ms = Volatile.Read(ref _lastFirstFrameMs);
            return ms < 0 ? snapshot : snapshot with { OverlayFirstFrameMs = ms };
        });
    }

    /// <summary>False once the OS reported the window as not click-through; the trail is a no-op from then on.</summary>
    public bool IsUsable => Volatile.Read(ref _usable) != 0;

    /// <summary>Frames drawn from the buffer since startup; one per render request.</summary>
    public int FramesRendered { get; private set; }

    public int PointsOnScreen => _canvas.PointCount;

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

    /// <summary>Applies the native styles before the window appears, shows it, and verifies them again afterwards (Avalonia rewrites them on Show).</summary>
    public override void Show()
    {
        if (!IsUsable)
        {
            return;
        }

        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle != 0)
        {
            _style.Apply(handle);
        }

        _visibleSince = DateTimeOffset.UtcNow;
        base.Show();
        if (VerifyOrHide("show"))
        {
            FitVirtualScreen();
        }
    }

    public void Dispose()
    {
        _watchdog.Stop();
        _healthRegistration?.Dispose();
    }

    /// <summary>The watchdog's check, with the clock injected for tests: a window still visible <see cref="IdleVisibleLimit"/> after the stroke ended is hidden and reported.</summary>
    internal void CheckIdle(DateTimeOffset now)
    {
        if (IsVisible && !_buffer.IsActive && now - _visibleSince > IdleVisibleLimit)
        {
            HideIdle();
            _log.Error(LogSource, "Overlay visible without a stroke, hidden", ("visibleForMs", (now - _visibleSince).TotalMilliseconds));
        }
    }

    private void RequestRender() => Dispatcher.UIThread.Post(RenderFrame, DispatcherPriority.Render);

    private void RenderFrame()
    {
        var frame = _buffer.Take();
        FramesRendered++;
        if (!frame.IsActive)
        {
            HideIdle();
            return;
        }

        if (frame.IsStrokeStart && frame.Points.Count > 0)
        {
            BeginStroke(frame);
            if (!IsVisible)
            {
                Show();
            }
        }

        if (IsVisible)
        {
            _canvas.SetPoints(frame.Points.Select(ToDip));
        }
    }

    private void HideIdle()
    {
        _canvas.SetPoints([]);
        if (IsVisible)
        {
            Hide();
        }
    }

    private bool VerifyOrHide(string reason)
    {
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        var report = _style.Apply(handle);
        if (OverlayStylePolicy.Decide(report) == OverlayStyleDecision.Show)
        {
            if (OverlayStylePolicy.HasCosmeticFault(report))
            {
                _log.Warning(LogSource, "Overlay style incomplete", ("reason", reason), ("style", report.Raw), ("noActivate", report.NoActivate), ("toolWindow", report.ToolWindow));
            }
            else
            {
                _log.Info(LogSource, "Overlay style verified", ("reason", reason), ("style", report.Raw));
            }

            return true;
        }

        Volatile.Write(ref _usable, 0);
        _buffer.End();
        HideIdle();
        _log.Error(LogSource, "Overlay not click-through, hidden", ("reason", reason), ("style", report.Raw));
        return false;
    }

    private void BeginStroke(TrailFrame frame)
    {
        var start = frame.Points[0];
        var screenScaling = Screens.ScreenFromPoint(new PixelPoint(start.X, start.Y))?.Scaling ?? RenderScaling;
        var widthDip = frame.Style.WidthPx * screenScaling / RenderScaling;
        var colour = frame.Style.Colour;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(frame.Style.Opacity * 255), colour.R, colour.G, colour.B));
        _canvas.SetPen(new Pen(brush, widthDip, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        _strokeStartedTicks = frame.StrokeStartedTicks;
        _firstFramePending = true;
    }

    private void OnRendered(int pointCount)
    {
        if (!_firstFramePending || pointCount < 2)
        {
            return;
        }

        _firstFramePending = false;
        var started = _strokeStartedTicks;
        RequestAnimationFrame(_ =>
        {
            var ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Volatile.Write(ref _lastFirstFrameMs, ms);
            _log.Info(LogSource, "Trail first frame", ("firstFrameMs", Math.Round(ms, 2)), ("points", pointCount));
        });
    }

    /// <summary>Covers the virtual screen: DIPs for Avalonia, then physical pixels through the port, because a DIP size is applied at one monitor's scale (observed 3072×1728 for a 3840×2160 monitor at 125 %).</summary>
    private void FitVirtualScreen()
    {
        var bounds = VirtualScreenBounds();
        Position = bounds.Position;
        Width = bounds.Width / RenderScaling;
        Height = bounds.Height / RenderScaling;
        var handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle != 0)
        {
            _style.Place(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    private PixelRect VirtualScreenBounds()
    {
        var screens = Screens.All;
        if (screens.Count == 0)
        {
            return FallbackBounds;
        }

        var bounds = screens[0].Bounds;
        for (var i = 1; i < screens.Count; i++)
        {
            bounds = bounds.Union(screens[i].Bounds);
        }

        return bounds;
    }

    private Point ToDip(CapturePoint point) => new((point.X - Position.X) / RenderScaling, (point.Y - Position.Y) / RenderScaling);
}
