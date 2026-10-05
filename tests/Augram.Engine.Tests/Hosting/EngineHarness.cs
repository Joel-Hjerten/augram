using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Engine.Hosting;
using Augram.Engine.Tests.Fakes;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// An <see cref="EngineHost"/> over fakes, with helpers to script raw input and wait for the worker.
/// The worker is a real thread, so assertions wait with <see cref="WaitFor"/> rather than sleeping.
/// </summary>
internal sealed class EngineHarness : IDisposable
{
    public const MouseButton StrokeButton = MouseButton.Right;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly List<EngineEvent> _events = [];

    public EngineHarness(EngineHostOptions? options = null, IReadOnlyList<Gesture>? gestures = null, RecognitionOptions? recognition = null)
    {
        Gestures = gestures ?? [LineGesture("right", 200, 0)];
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
            },
            () => Gestures,
            () => recognition ?? RecognitionOptions.Default,
            options ?? new EngineHostOptions(StrokeButton, TickInterval: TimeSpan.FromMilliseconds(1), HealthPollInterval: TimeSpan.FromHours(1)));
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

    public EngineHost Host { get; }

    public IReadOnlyList<Gesture> Gestures { get; set; }

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

    public static void WaitFor(Func<bool> condition, string what)
    {
        if (!SpinWait.SpinUntil(condition, Timeout))
        {
            throw new TimeoutException($"timed out waiting for {what}");
        }
    }

    public void WaitForState(CaptureState state) => WaitFor(() => Host.State == state, $"state {state} (now {Host.State})");

    public void WaitForEvents(int count) => WaitFor(() => Events.Count >= count, $"{count} engine events (have {Events.Count})");

    public void Dispose() => Host.Dispose();
}
