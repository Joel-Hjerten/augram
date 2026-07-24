// Augram risk spike: capture a chosen mouse button globally, suppress it,
// collect the stroke, and replay the original click when the stroke is too
// small to be a gesture. Validates the suppress-then-replay loop that the
// whole app depends on (see docs/handoff.md §5).
//
// Usage:
//   dotnet run -- identify     print every button press so you can find button numbers
//   dotnet run -- <1-5>        run gesture capture with that button (default: 2 = right)
//
// While running: hold the gesture button and draw. A real stroke is reported
// and saved to ./strokes/*.json; a motionless press is replayed as a normal
// click (context menus etc. must still work). Ctrl+C exits and prints the
// worst observed hook-handler latency.

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using SharpHook;
using SharpHook.Data;

const double ClickDistanceThreshold = 10.0; // px of displacement before a press counts as a gesture

if (args.Length > 0 && args[0].Equals("identify", StringComparison.OrdinalIgnoreCase))
{
    RunIdentify();
    return;
}

var gestureButton = MouseButton.Button2; // right button (verify with `identify` — libuiohook numbering)
if (args.Length > 0 && int.TryParse(args[0], out var buttonNumber) && buttonNumber is >= 1 and <= 5)
    gestureButton = (MouseButton)buttonNumber;

Console.WriteLine($"Gesture button: {gestureButton}. Hold it and draw; motionless presses pass through. Ctrl+C to exit.");

// Everything heavier than appending a point happens off the hook thread,
// via this channel. The hook thread only records points and enqueues work.
var work = Channel.CreateUnbounded<StrokeResult>(new UnboundedChannelOptions { SingleReader = true });
var simulator = new EventSimulator();

var worker = Task.Run(async () =>
{
    await foreach (var result in work.Reader.ReadAllAsync())
    {
        var lagMs = (Stopwatch.GetTimestamp() - result.EnqueuedAt) * 1000.0 / Stopwatch.Frequency;

        if (result.IsClick)
        {
            // Not a gesture: replay the original press+release where it started.
            // IsEventSimulated keeps the hook from re-capturing these.
            simulator.SimulateMousePress(result.DownX, result.DownY, result.Button);
            simulator.SimulateMouseRelease(result.DownX, result.DownY, result.Button);
            Console.WriteLine(
                $"  click passthrough: {result.Button} replayed at ({result.DownX},{result.DownY}), " +
                $"held {result.DurationMs:F0} ms, replay lag {lagMs:F1} ms");
        }
        else
        {
            var p = result.Points;
            short minX = short.MaxValue, minY = short.MaxValue, maxX = short.MinValue, maxY = short.MinValue;
            double pathLength = 0;
            for (var i = 0; i < p.Count; i++)
            {
                minX = Math.Min(minX, p[i].X); maxX = Math.Max(maxX, p[i].X);
                minY = Math.Min(minY, p[i].Y); maxY = Math.Max(maxY, p[i].Y);
                if (i > 0)
                {
                    double dx = p[i].X - p[i - 1].X, dy = p[i].Y - p[i - 1].Y;
                    pathLength += Math.Sqrt(dx * dx + dy * dy);
                }
            }

            Directory.CreateDirectory("strokes");
            var file = Path.Combine("strokes", $"stroke-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine(
                $"  gesture: {p.Count} points, {result.DurationMs:F0} ms, " +
                $"path {pathLength:F0} px, bbox {maxX - minX}x{maxY - minY} -> {file}");
        }
    }
});

using var hook = new SimpleGlobalHook(GlobalHookType.Mouse);

// Capture state. All hook handlers run synchronously on the one hook thread,
// so no locking is needed.
var capturing = false;
short downX = 0, downY = 0;
var strokeTimer = new Stopwatch();
var points = new List<StrokePoint>(4096);
long worstHandlerTicks = 0;

hook.MousePressed += (_, e) =>
{
    if (e.IsEventSimulated || e.Data.Button != gestureButton || capturing)
        return;
    var t0 = Stopwatch.GetTimestamp();

    e.SuppressEvent = true;
    capturing = true;
    downX = e.Data.X;
    downY = e.Data.Y;
    points = new List<StrokePoint>(4096) { new(e.Data.X, e.Data.Y, 0) };
    strokeTimer.Restart();

    worstHandlerTicks = Math.Max(worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
};

hook.MouseMoved += OnMove;
hook.MouseDragged += OnMove;

void OnMove(object? sender, MouseHookEventArgs e)
{
    if (!capturing || e.IsEventSimulated)
        return;
    var t0 = Stopwatch.GetTimestamp();

    points.Add(new StrokePoint(e.Data.X, e.Data.Y, strokeTimer.Elapsed.TotalMilliseconds));

    worstHandlerTicks = Math.Max(worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
}

hook.MouseReleased += (_, e) =>
{
    if (e.IsEventSimulated || e.Data.Button != gestureButton || !capturing)
        return;
    var t0 = Stopwatch.GetTimestamp();

    e.SuppressEvent = true;
    capturing = false;
    points.Add(new StrokePoint(e.Data.X, e.Data.Y, strokeTimer.Elapsed.TotalMilliseconds));

    double maxDisplacement = 0;
    foreach (var pt in points)
    {
        double dx = pt.X - downX, dy = pt.Y - downY;
        maxDisplacement = Math.Max(maxDisplacement, Math.Sqrt(dx * dx + dy * dy));
    }

    work.Writer.TryWrite(new StrokeResult(
        gestureButton, downX, downY,
        strokeTimer.Elapsed.TotalMilliseconds,
        maxDisplacement < ClickDistanceThreshold,
        points)
    {
        EnqueuedAt = Stopwatch.GetTimestamp(),
    });

    worstHandlerTicks = Math.Max(worstHandlerTicks, Stopwatch.GetTimestamp() - t0);
};

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    hook.Dispose();
};

hook.Run(); // blocks until disposed

work.Writer.Complete();
await worker;
Console.WriteLine($"Worst hook-handler time: {worstHandlerTicks * 1000.0 / Stopwatch.Frequency:F3} ms");

static void RunIdentify()
{
    Console.WriteLine("Press mouse buttons to identify their numbers. Nothing is suppressed. Ctrl+C to exit.");
    using var hook = new SimpleGlobalHook(GlobalHookType.Mouse);
    hook.MousePressed += (_, e) =>
        Console.WriteLine($"  {e.Data.Button} (#{(int)e.Data.Button}) at ({e.Data.X},{e.Data.Y}) clicks={e.Data.Clicks}");
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        hook.Dispose();
    };
    hook.Run();
}

record StrokeResult(
    MouseButton Button,
    short DownX,
    short DownY,
    double DurationMs,
    bool IsClick,
    List<StrokePoint> Points)
{
    public long EnqueuedAt { get; init; }
}

record struct StrokePoint(short X, short Y, double T);
