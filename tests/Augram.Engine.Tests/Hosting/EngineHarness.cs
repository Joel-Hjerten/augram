using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// An <see cref="EngineHost"/> over fakes, with helpers to script raw input and wait for the worker.
/// The worker and the executor are real threads, so assertions wait with <see cref="WaitFor"/> rather
/// than sleeping. Without a mapping document there is no executor (M1 behaviour: recognise and
/// report); with one, the window fakes and the settle delay drive the command executor.
/// </summary>
internal sealed class EngineHarness : IDisposable
{
    public const MouseButton StrokeButton = MouseButton.Right;
    // Generous: only a failing test waits this long.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private readonly object _gate = new();
    private readonly List<EngineEvent> _events = [];

    public EngineHarness(
        EngineHostOptions? options = null,
        IReadOnlyList<Gesture>? gestures = null,
        RecognitionOptions? recognition = null,
        MappingDocument? mapping = null,
        Func<EngineEvent, bool>? intercept = null,
        int? settleDelayMs = null)
    {
        Gestures = gestures ?? [LineGesture("right", 200, 0)];
        Mapping = mapping;
        options ??= new EngineHostOptions(StrokeButton, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1));
        if (settleDelayMs is { } settle)
        {
            options = options with { SettleDelayMs = settle };
        }

        Host = new EngineHost(
            new EnginePorts
            {
                Input = Source,
                Simulator = Simulator,
                Clock = Clock,
                Log = Log,
                Trail = Trail,
                RecognitionLog = RecognitionLog,
                Health = Health,
                SystemEvents = SystemEvents,
                Windows = Windows,
                WindowOperations = WindowOperations,
                Mapping = mapping is null ? null : () => Mapping!,
                Intercept = intercept,
            },
            () => Gestures,
            () => recognition ?? RecognitionOptions.Default,
            options);
        Host.EventRaised += (_, e) =>
        {
            lock (_gate)
            {
                _events.Add(e);
            }
        };
        Host.Start();
    }

    public FakeInputSource Source { get; } = new();

    public FakeInputSimulator Simulator { get; } = new();

    public FakeClock Clock { get; } = new();

    public ListEventLog Log { get; } = new();

    public RecordingTrail Trail { get; } = new();

    public RecognitionLog RecognitionLog { get; } = new();

    public HealthRegistry Health { get; } = new();

    public FakeWindowSystem Windows { get; } = new();

    public FakeWindowOperations WindowOperations { get; } = new();

    /// <summary>Session, power and foreground events a test raises (the watch and the hook health monitor listen).</summary>
    public FakeSystemEvents SystemEvents { get; } = new();

    public EngineHost Host { get; }

    public IReadOnlyList<Gesture> Gestures { get; set; }

    /// <summary>The document the executor resolves against; read per request, so a test may replace it between strokes.</summary>
    public MappingDocument? Mapping { get; set; }

    public IReadOnlyList<EngineEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public static Gesture LineGesture(string name, int dx, int dy)
    {
        var points = Enumerable.Range(0, 21).Select(i => new GesturePoint(dx * i / 20.0, dy * i / 20.0));
        return new Gesture(GestureId.New(), name, true, [new GestureSample(points)]);
    }

    public bool Down(MouseButton button, int x, int y, long t, KeyModifiers modifiers = KeyModifiers.None)
        => Source.Deliver(RawInput.ButtonDown(button, x, y, t, modifiers));

    public bool Up(MouseButton button, int x, int y, long t) => Source.Deliver(RawInput.ButtonUp(button, x, y, t));

    public void Move(int x, int y, long t) => Source.Deliver(RawInput.Move(x, y, t));

    public bool Wheel(WheelDirection direction, int x, int y, long t) => Source.Deliver(RawInput.WheelTick(direction, x, y, t));

    /// <summary>A key going down (again, for an auto-repeat); returns the hook's decision.</summary>
    public bool KeyDown(KeyCode key, long t, KeyModifiers modifiers = KeyModifiers.None) => Source.Deliver(RawInput.KeyDown(key, t, modifiers));

    public bool KeyUp(KeyCode key, long t, KeyModifiers modifiers = KeyModifiers.None) => Source.Deliver(RawInput.KeyUp(key, t, modifiers));

    /// <summary>A press at (100,100), moves in a straight line by (dx, dy) in 20 steps, release; returns the press and release decisions.</summary>
    public (bool Down, bool Up) Stroke(int dx, int dy, long startMs = 0)
    {
        var down = Down(StrokeButton, 100, 100, startMs);
        for (var i = 1; i <= 20; i++)
        {
            Move(100 + (dx * i / 20), 100 + (dy * i / 20), startMs + (i * 10));
        }

        var up = Up(StrokeButton, 100 + dx, 100 + dy, startMs + 210);
        return (down, up);
    }

    /// <summary>Polls every 10 ms rather than spinning, so parallel tests do not starve the worker threads they wait on (2026-10-07, Windows CI).</summary>
    public static void WaitFor(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"timed out waiting for {what}");
            }

            Thread.Sleep(PollInterval);
        }
    }

    public void WaitForState(CaptureState state) => WaitFor(() => Host.State == state, $"state {state} (now {Host.State})");

    public void WaitForEvents(int count) => WaitFor(() => Events.Count >= count, $"{count} engine events (have {Events.Count})");

    /// <summary>The recognition log entry of a recognised gesture lands after the event is raised (by the worker or the executor), so wait for it before reading it.</summary>
    public void WaitForRecognitionLog(int count) => WaitFor(() => RecognitionLog.Count >= count, $"{count} recognition log entries (have {RecognitionLog.Count})");

    /// <summary>The worker logs after it acts (click injected, event raised), so a test that waited for the act must also wait for the line.</summary>
    public void WaitForLog(string source, string message) => WaitFor(() => Log.Has(source, message), $"log line {source}/{message}");

    public void Dispose() => Host.Dispose();
}
