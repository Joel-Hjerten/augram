// B2: a pre-created transparent, click-through, topmost, non-activating Avalonia window that
// covers the virtual screen and draws the stroke trail while the RIGHT button is held.
//
// Data flow (invariant 1 from CLAUDE.md): hook thread -> channel -> worker -> UI thread.
// The hook handlers only enqueue; the worker batches points into a pending list and posts
// at most one UI flush at a time, so the overlay redraws once per UI-thread turn.
using System.Diagnostics;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Data;

namespace Augram.Spike2;

internal static class OverlayMode
{
    public const double ClickThresholdPx = 30.0;
    public const double PenWidthPx = 5.0;
    public static readonly MouseButton StrokeButton = MouseButton.Button2; // right; SP.net owns Middle on Joel's machine

    public static bool KeepShown { get; private set; }
    public static bool Layered { get; private set; }
    /// <summary>Agent self-test: accept SharpHook-simulated input as if it were physical and draw one synthetic stroke.</summary>
    public static bool SelfTest { get; private set; }
    public static CancellationTokenSource? Cts { get; private set; }

    public static int Run(string[] args, CancellationTokenSource cts)
    {
        KeepShown = args.Contains("keep", StringComparer.OrdinalIgnoreCase);
        Layered = args.Contains("layered", StringComparer.OrdinalIgnoreCase);
        SelfTest = args.Contains("selftest", StringComparer.OrdinalIgnoreCase);
        Cts = cts;

        Log.Instructions(
            "OVERLAY (B2). Hold the RIGHT mouse button and draw anywhere: a green 5 px trail should appear under the cursor " +
            "within one frame, on every monitor, without the window underneath losing focus. Release: a stroke shorter than " +
            "30 px is replayed as a normal right click (context menus must still work), longer strokes just clear. Test over " +
            "a borderless-fullscreen game and across both monitors (different DPI: the pen should look the same width on each). " +
            "Each stroke logs first-frame latency, per-batch render time, and whether the overlay ever became the foreground " +
            $"window. Options: 'keep' leaves the window shown-but-empty when idle (now: {(KeepShown ? "keep" : "hide when idle")}), " +
            $"'layered' adds WS_EX_LAYERED (now: {(Layered ? "on" : "off")}). Ctrl+C in this console to stop.");

        return AppBuilder.Configure<OverlayApp>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}

internal sealed class OverlayApp : Application
{
    private OverlayController? _controller;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _controller = new OverlayController(desktop);
            _controller.Start();
            desktop.Exit += (_, _) => _controller.Stop();
            OverlayMode.Cts!.Token.Register(() => Dispatcher.UIThread.Post(() => desktop.Shutdown()));
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal enum StrokeEventKind { Down, Point, Up }

internal readonly record struct StrokeEvent(StrokeEventKind Kind, short X, short Y, long Ticks);

/// <summary>Owns the hook, the channel, the worker, and the overlay window.</summary>
internal sealed class OverlayController
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly Channel<StrokeEvent> _events = Channel.CreateUnbounded<StrokeEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SimpleGlobalHook _hook = new(GlobalHookType.Mouse);
    private readonly EventSimulator _simulator = new();
    private OverlayWindow? _window;
    private Thread? _hookThread;
    private Task? _worker;

    // Hook-thread state (single thread, no locking).
    private bool _capturing;
    private long _worstHandlerTicks;

    public OverlayController(IClassicDesktopStyleApplicationLifetime desktop) => _desktop = desktop;

    public void Start()
    {
        _window = new OverlayWindow();
        var sw = Stopwatch.StartNew();
        _window.Show(); // pre-create: the HWND and the compositor surface exist before the first stroke
        Log.Info($"overlay window pre-created and shown in {sw.ElapsedMilliseconds} ms");

        _hook.MousePressed += OnPressed;
        _hook.MouseMoved += OnMoved;
        _hook.MouseDragged += OnMoved;
        _hook.MouseReleased += OnReleased;
        _hookThread = new Thread(() =>
        {
            try
            { _hook.Run(); }
            catch (HookException e) { Log.Info($"mouse hook failed: {e.Result} {e.Message}"); }
        })
        { IsBackground = true, Name = "mouse-hook" };
        _hookThread.Start();
        _worker = Task.Run(WorkerLoop);
        Log.Info($"hook running; hold {OverlayMode.StrokeButton} and draw");
        if (OverlayMode.SelfTest)
        {
            Task.Run(SelfTestStrokes);
        }
    }

    /// <summary>Three synthetic diagonal strokes from the current cursor position, then exit. Moves the real cursor briefly.</summary>
    private async Task SelfTestStrokes()
    {
        await Task.Delay(2000);
        Native.GetCursorPos(out var origin);
        for (var stroke = 0; stroke < 3; stroke++)
        {
            Log.Info($"selftest: stroke {stroke + 1}/3 starting at ({origin.X},{origin.Y})");
            short x = (short)origin.X, y = (short)origin.Y;
            _simulator.SimulateMousePress(x, y, OverlayMode.StrokeButton);
            for (var i = 0; i < 40; i++)
            {
                x += 6;
                y += 4;
                _simulator.SimulateMouseMovement(x, y);
                await Task.Delay(8);
            }
            _simulator.SimulateMouseRelease(x, y, OverlayMode.StrokeButton);
            await Task.Delay(700);
        }
        _simulator.SimulateMouseMovement((short)origin.X, (short)origin.Y);
        await Task.Delay(500);
        Log.Info("selftest: done, shutting down");
        OverlayMode.Cts?.Cancel();
    }

    private static bool IsForeign(HookEventArgs e) => e.IsEventSimulated && !OverlayMode.SelfTest;

    public void Stop()
    {
        _hook.Dispose();
        _events.Writer.TryComplete();
        _hookThread?.Join(3000);
        _worker?.Wait(3000);
        Log.Info($"hook disposed; worst hook-handler time {Ms(_worstHandlerTicks):F3} ms");
    }

    private void OnPressed(object? sender, MouseHookEventArgs e)
    {
        if (IsForeign(e) || e.Data.Button != OverlayMode.StrokeButton || _capturing)
        {
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        e.SuppressEvent = true;
        _capturing = true;
        _events.Writer.TryWrite(new StrokeEvent(StrokeEventKind.Down, e.Data.X, e.Data.Y, t0));
        _worstHandlerTicks = Math.Max(_worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
    }

    private void OnMoved(object? sender, MouseHookEventArgs e)
    {
        if (!_capturing || IsForeign(e))
        {
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        _events.Writer.TryWrite(new StrokeEvent(StrokeEventKind.Point, e.Data.X, e.Data.Y, t0));
        _worstHandlerTicks = Math.Max(_worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
    }

    private void OnReleased(object? sender, MouseHookEventArgs e)
    {
        if (IsForeign(e) || e.Data.Button != OverlayMode.StrokeButton || !_capturing)
        {
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        e.SuppressEvent = true;
        _capturing = false;
        _events.Writer.TryWrite(new StrokeEvent(StrokeEventKind.Up, e.Data.X, e.Data.Y, t0));
        _worstHandlerTicks = Math.Max(_worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
    }

    private async Task WorkerLoop()
    {
        short downX = 0, downY = 0;
        double maxDisplacement = 0;
        var points = 0;

        await foreach (var ev in _events.Reader.ReadAllAsync())
        {
            var window = _window!;
            switch (ev.Kind)
            {
                case StrokeEventKind.Down:
                    downX = ev.X;
                    downY = ev.Y;
                    maxDisplacement = 0;
                    points = 1;
                    window.BeginStroke(ev);
                    break;

                case StrokeEventKind.Point:
                    points++;
                    maxDisplacement = Math.Max(maxDisplacement, Dist(ev.X - downX, ev.Y - downY));
                    window.AddPoint(ev);
                    break;

                case StrokeEventKind.Up:
                    points++;
                    maxDisplacement = Math.Max(maxDisplacement, Dist(ev.X - downX, ev.Y - downY));
                    var isClick = maxDisplacement < OverlayMode.ClickThresholdPx;
                    await Dispatcher.UIThread.InvokeAsync(() => window.EndStroke(ev, points, maxDisplacement, isClick, _worstHandlerTicks));
                    if (isClick && !OverlayMode.SelfTest)
                    {
                        // Replay the original click now that the overlay is out of the way (worker thread, never the hook thread).
                        _simulator.SimulateMousePress(downX, downY, OverlayMode.StrokeButton);
                        _simulator.SimulateMouseRelease(downX, downY, OverlayMode.StrokeButton);
                        Log.Info($"  click passthrough: right click replayed at ({downX},{downY}), " +
                                 $"{Ms(Stopwatch.GetTimestamp() - ev.Ticks):F1} ms after the physical release");
                    }
                    break;
            }
        }
    }

    private static double Dist(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);
    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>The transparent full-virtual-screen window. UI-thread methods unless stated.</summary>
internal sealed class OverlayWindow : Window
{
    private readonly StrokeCanvas _canvas = new();
    private readonly DispatcherTimer _foregroundProbe;
    private nint _hwnd;
    private bool _initialized;
    private (int X, int Y, int W, int H) _virtual;

    // Pending points from the worker, flushed once per UI turn.
    private readonly object _gate = new();
    private List<StrokeEvent> _pending = new(256);
    private bool _flushScheduled;

    // Per-stroke measurements.
    private long _downTicks, _firstMoveTicks, _firstSegmentRenderTicks, _firstFrameTicks;
    private double _showMs;
    private int _flushes;
    private bool _tookForeground;
    private bool _active;

    public OverlayWindow()
    {
        Title = "Augram overlay spike";
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

        _virtual = Native.VirtualScreen();
        Position = new PixelPoint(_virtual.X, _virtual.Y);
        Width = _virtual.W;
        Height = _virtual.H;
        Content = _canvas;
        _canvas.Rendered += OnCanvasRendered;

        _foregroundProbe = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => ProbeForeground());
        Opened += (_, _) => OnOpened();
    }

    private void OnOpened()
    {
        // Avalonia 11.3 raises Opened on every Show() after a Hide(), not only the first time.
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        _hwnd = TryGetPlatformHandle()?.Handle ?? 0;
        var (before, after) = ApplyNativeStyles();
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, _virtual.X, _virtual.Y, _virtual.W, _virtual.H,
            Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);

        Log.Info($"overlay hwnd=0x{_hwnd:X} exstyle 0x{before:X} -> 0x{after:X}; transparency hint Transparent, actual {ActualTransparencyLevel}; " +
                 $"RenderScaling {RenderScaling}; virtual screen {_virtual.W}x{_virtual.H} at ({_virtual.X},{_virtual.Y}); " +
                 $"Position {Position}, ClientSize {ClientSize}");
        foreach (var s in Screens.All)
        {
            Log.Info($"  screen: bounds {s.Bounds} scaling {s.Scaling} primary={s.IsPrimary}");
        }
        Log.Info($"  Win32 DPI at virtual origin {Native.DpiAt(_virtual.X, _virtual.Y)}, at (0,0) {Native.DpiAt(0, 0)}");

        if (!OverlayMode.KeepShown)
        {
            Hide();
            Log.Info("overlay hidden until the first stroke (hide-when-idle)");
        }
    }

    /// <summary>
    /// Ex-styles Avalonia does not expose. Must be re-applied after every Show(): the Win32 backend
    /// rewrites GWL_EXSTYLE when showing, which drops TRANSPARENT/NOACTIVATE/TOOLWINDOW (observed 11.3.22).
    /// Returns the style before and after.
    /// </summary>
    private (long Before, long After) ApplyNativeStyles()
    {
        var ex = (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE);
        var want = ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
        if (OverlayMode.Layered)
        {
            want |= Native.WS_EX_LAYERED;
        }
        if (want != ex)
        {
            Native.SetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE, (nint)want);
            if (OverlayMode.Layered)
            {
                Native.SetLayeredWindowAttributes(_hwnd, 0, 255, Native.LWA_ALPHA);
            }
        }
        return (ex, (long)Native.GetWindowLongPtr(_hwnd, Native.GWL_EXSTYLE));
    }

    // ---- called from the worker thread ----

    public void BeginStroke(StrokeEvent down)
    {
        lock (_gate)
        {
            _pending.Clear();
            _pending.Add(down);
            _flushScheduled = true;
        }
        Dispatcher.UIThread.Post(Flush, DispatcherPriority.Render);
    }

    public void AddPoint(StrokeEvent point)
    {
        bool schedule;
        lock (_gate)
        {
            _pending.Add(point);
            schedule = !_flushScheduled;
            _flushScheduled = true;
        }
        if (schedule)
        {
            Dispatcher.UIThread.Post(Flush, DispatcherPriority.Render);
        }
    }

    // ---- UI thread ----

    private void Flush()
    {
        List<StrokeEvent> batch;
        lock (_gate)
        {
            batch = _pending;
            _pending = new List<StrokeEvent>(256);
            _flushScheduled = false;
        }
        if (batch.Count == 0)
        {
            return;
        }

        foreach (var ev in batch)
        {
            if (ev.Kind == StrokeEventKind.Down)
            {
                StartOnUi(ev);
            }
            else if (_active)
            {
                if (_firstMoveTicks == 0)
                {
                    _firstMoveTicks = ev.Ticks;
                }
                _canvas.Add(ToDip(ev.X, ev.Y));
            }
        }
        _flushes++;
        _canvas.InvalidateVisual();
    }

    private void StartOnUi(StrokeEvent down)
    {
        _active = true;
        _downTicks = down.Ticks;
        _firstMoveTicks = _firstSegmentRenderTicks = _firstFrameTicks = 0;
        _flushes = 0;
        _tookForeground = false;
        _showMs = 0;

        var dpi = Native.DpiAt(down.X, down.Y);
        var penDip = OverlayMode.PenWidthPx * (dpi / 96.0) / RenderScaling;
        _canvas.Begin(penDip, ToDip(down.X, down.Y));

        var showNote = ", window already shown";
        if (!IsVisible)
        {
            var sw = Stopwatch.StartNew();
            Show();
            var (before, after) = ApplyNativeStyles();
            _showMs = sw.Elapsed.TotalMilliseconds;
            showNote = $", Show() took {_showMs:F1} ms, exstyle after Show 0x{before:X}{(before == after ? " (intact)" : $" -> restored 0x{after:X}")}";
        }
        Log.Info($"stroke start at ({down.X},{down.Y}) monitor dpi {dpi}, window scale {RenderScaling:F2}, pen {penDip:F2} dip{showNote}");
        ProbeForeground();
        _foregroundProbe.Start();
    }

    private void OnCanvasRendered(int pointCount, long renderTicks)
    {
        if (_firstSegmentRenderTicks == 0 && pointCount >= 2)
        {
            _firstSegmentRenderTicks = renderTicks;
            RequestAnimationFrame(_ =>
            {
                if (_firstFrameTicks == 0)
                {
                    _firstFrameTicks = Stopwatch.GetTimestamp();
                }
            });
        }
    }

    public void EndStroke(StrokeEvent up, int points, double maxDisplacement, bool isClick, long worstHookTicks)
    {
        _foregroundProbe.Stop();
        ProbeForeground();
        lock (_gate)
        {
            _pending.Clear();
            _flushScheduled = false;
        }
        _active = false;

        var stats = _canvas.Stats;
        var firstSeg = _firstSegmentRenderTicks == 0 ? "never rendered a segment" :
            $"first segment Render() {Ms(_firstSegmentRenderTicks - _downTicks):F1} ms after button-down, " +
            $"{(_firstMoveTicks == 0 ? "n/a" : Ms(_firstSegmentRenderTicks - _firstMoveTicks).ToString("F1"))} ms after first move";
        var firstFrame = _firstFrameTicks == 0 ? "" :
            $"; next-frame callback {Ms(_firstFrameTicks - _downTicks):F1} ms after button-down";

        Log.Info($"stroke end: {points} points, max displacement {maxDisplacement:F0} px -> {(isClick ? "CLICK" : "GESTURE (cleared)")}; " +
                 $"{_flushes} UI batches, {stats.Renders} renders, render avg {stats.AvgMs:F3} ms max {stats.MaxMs:F3} ms; " +
                 $"{firstSeg}{firstFrame}; worst hook handler {Ms(worstHookTicks):F3} ms; " +
                 $"overlay took foreground: {(_tookForeground ? "YES (BAD)" : "no")}");

        _canvas.Clear();
        _canvas.InvalidateVisual();
        if (!OverlayMode.KeepShown)
        {
            Hide();
        }
    }

    private void ProbeForeground()
    {
        if (_tookForeground || _hwnd == 0)
        {
            return;
        }
        if (Native.GetForegroundWindow() == _hwnd)
        {
            _tookForeground = true;
            Log.Info("  OVERLAY TOOK FOREGROUND (BAD): GetForegroundWindow() == overlay hwnd");
        }
    }

    private Point ToDip(int x, int y) => new((x - Position.X) / RenderScaling, (y - Position.Y) / RenderScaling);

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>Draws the polyline. Rebuilds the geometry each render; cheap for a few hundred points.</summary>
internal sealed class StrokeCanvas : Control
{
    private readonly List<Point> _points = new(1024);
    private IPen _pen = new Pen(Brushes.Transparent);
    private int _renders;
    private long _totalRenderTicks, _maxRenderTicks;

    public event Action<int, long>? Rendered;

    public (int Renders, double AvgMs, double MaxMs) Stats => (
        _renders,
        _renders == 0 ? 0 : _totalRenderTicks * 1000.0 / Stopwatch.Frequency / _renders,
        _maxRenderTicks * 1000.0 / Stopwatch.Frequency);

    public void Begin(double penWidthDip, Point first)
    {
        _points.Clear();
        _points.Add(first);
        _pen = new Pen(new SolidColorBrush(Color.FromArgb(128, 0x20, 0xC0, 0x40)), penWidthDip,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _renders = 0;
        _totalRenderTicks = _maxRenderTicks = 0;
    }

    public void Add(Point p) => _points.Add(p);

    public void Clear() => _points.Clear();

    public override void Render(DrawingContext context)
    {
        if (_points.Count < 2)
        {
            return;
        }
        var t0 = Stopwatch.GetTimestamp();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_points[0], false);
            for (var i = 1; i < _points.Count; i++)
            {
                ctx.LineTo(_points[i]);
            }
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, _pen, geometry);

        var elapsed = Stopwatch.GetTimestamp() - t0;
        _renders++;
        _totalRenderTicks += elapsed;
        _maxRenderTicks = Math.Max(_maxRenderTicks, elapsed);
        Rendered?.Invoke(_points.Count, Stopwatch.GetTimestamp());
    }
}
