using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// Hotkey capture (F5) through the fake input source: keys suppressed system-wide only while armed, every
/// key event reported on the worker, released on dispose, watchdog, replacement, hook reset and stop, owed
/// releases still swallowed afterwards (A19 for keys), the mouse never touched. No real hook anywhere.
/// </summary>
public sealed class KeyCaptureTests
{
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(1);

    [Fact]
    public void KeysAreSwallowedAndReportedOnlyWhileArmed()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        Assert.False(Key(harness, down: true, KeyCode.T, 0));
        Assert.False(Key(harness, down: false, KeyCode.T, 10));

        var capture = harness.Host.CaptureKeys(seen.Add, Long);
        Assert.True(harness.Host.IsCapturingKeys);
        Assert.True(Key(harness, down: true, KeyCode.LeftControl, 20));
        Assert.True(Key(harness, down: true, KeyCode.T, 30, KeyModifiers.Control));
        Assert.True(Key(harness, down: false, KeyCode.T, 40, KeyModifiers.Control));
        Assert.True(Key(harness, down: false, KeyCode.LeftControl, 50));
        seen.WaitFor(4);
        capture.Dispose();

        Assert.False(harness.Host.IsCapturingKeys);
        Assert.False(Key(harness, down: true, KeyCode.T, 60));
        Assert.False(Key(harness, down: false, KeyCode.T, 70));
        KeyCaptureEvent[] expected =
        [
            KeyCaptureEvent.KeyDown(KeyCode.LeftControl),
            KeyCaptureEvent.KeyDown(KeyCode.T, KeyModifiers.Control),
            KeyCaptureEvent.KeyUp(KeyCode.T, KeyModifiers.Control),
            KeyCaptureEvent.KeyUp(KeyCode.LeftControl),
        ];
        Barrier(harness);
        Assert.Equal(expected, seen.Events);
        Assert.Equal(60_000.0, harness.Log.Single(LogSources.Engine, "Key capture armed").Properties!.Single(p => p.Key == "idleTimeoutMs").Value);
        Assert.Equal(KeyCaptureEvent.DisposedReason, harness.Log.Single(LogSources.Engine, "Key capture released").Properties!.Single(p => p.Key == "reason").Value);
    }

    [Fact]
    public void AKeyPressedDuringCaptureHasItsReleaseSwallowedAfterward()
    {
        using var harness = new EngineHarness();
        var capture = harness.Host.CaptureKeys(_ => { }, Long);
        Assert.True(Key(harness, down: true, KeyCode.LeftShift, 0));
        capture.Dispose();

        Assert.True(Key(harness, down: true, KeyCode.LeftShift, 400));
        Assert.True(Key(harness, down: false, KeyCode.LeftShift, 450));
        Assert.False(Key(harness, down: true, KeyCode.LeftShift, 500));
        Assert.False(Key(harness, down: false, KeyCode.LeftShift, 510));
    }

    [Fact]
    public void AKeyHeldFromBeforeCaptureStillReachesTheOs()
    {
        using var harness = new EngineHarness();
        Assert.False(Key(harness, down: true, KeyCode.LeftAlt, 0));
        using var capture = harness.Host.CaptureKeys(_ => { }, Long);

        Assert.False(Key(harness, down: true, KeyCode.LeftAlt, 400));
        Assert.False(Key(harness, down: false, KeyCode.LeftAlt, 450));
        Assert.True(Key(harness, down: true, KeyCode.Tab, 460));
    }

    [Fact]
    public void TheWatchdogReleasesAfterTheIdleTimeoutAndSaysWhy()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        using var capture = harness.Host.CaptureKeys(seen.Add, TimeSpan.FromMilliseconds(100));

        seen.WaitFor(1);
        harness.WaitForLog(LogSources.Engine, "Key capture released");

        Assert.False(harness.Host.IsCapturingKeys);
        Assert.False(Key(harness, down: true, KeyCode.L, 0, KeyModifiers.Meta));
        Assert.Equal(KeyCaptureEvent.Released(KeyCaptureEvent.IdleTimeoutReason), Assert.Single(seen.Events));
    }

    [Fact]
    public void KeyEventsKeepTheWatchdogAway()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        using var capture = harness.Host.CaptureKeys(seen.Add, TimeSpan.FromMilliseconds(1000));

        for (var i = 0; i < 16; i++)
        {
            Key(harness, down: i % 2 == 0, KeyCode.A, i * 100);
            Thread.Sleep(100);
        }

        Assert.True(harness.Host.IsCapturingKeys);
        Assert.DoesNotContain(seen.Events, e => e.Kind == KeyCaptureEventKind.Released);
    }

    [Fact]
    public void TheMouseIsNeverAffected()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        using var capture = harness.Host.CaptureKeys(seen.Add, Long);

        Assert.False(harness.Down(MouseButton.X1, 10, 10, 0));
        Assert.False(harness.Up(MouseButton.X1, 10, 10, 10));
        Assert.False(harness.Wheel(WheelDirection.Up, 10, 10, 20));
        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 30));
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 40));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.True(harness.Host.IsCapturingKeys);
        Barrier(harness);
        Assert.Empty(seen.Events);
    }

    [Fact]
    public void ASecondCaptureReplacesTheFirst()
    {
        using var harness = new EngineHarness();
        var first = new Recorder();
        var second = new Recorder();
        var a = harness.Host.CaptureKeys(first.Add, Long);
        var b = harness.Host.CaptureKeys(second.Add, Long);

        first.WaitFor(1);
        Assert.True(Key(harness, down: true, KeyCode.Escape, 0));
        second.WaitFor(1);
        a.Dispose();
        Assert.True(harness.Host.IsCapturingKeys);
        b.Dispose();

        Assert.False(harness.Host.IsCapturingKeys);
        Assert.Equal([KeyCaptureEvent.Released(KeyCaptureEvent.ReplacedReason)], first.Events);
        Assert.Equal([KeyCaptureEvent.KeyDown(KeyCode.Escape)], second.Events);
    }

    [Fact]
    public void AHookResetReleasesAndForgetsWhatTheOldHookSaw()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        using var capture = harness.Host.CaptureKeys(seen.Add, Long);
        Assert.True(Key(harness, down: true, KeyCode.LeftControl, 0));
        seen.WaitFor(1);

        harness.Source.ReportLost();
        harness.Host.Health.Poll();
        seen.WaitFor(2);

        Assert.False(harness.Host.IsCapturingKeys);
        Assert.Equal(KeyCaptureEventKind.Released, seen.Events[^1].Kind);
        Assert.StartsWith(KeyCaptureEvent.HookResetReason, seen.Events[^1].Reason, StringComparison.Ordinal);
        Assert.False(Key(harness, down: false, KeyCode.LeftControl, 10));
        Assert.False(Key(harness, down: true, KeyCode.T, 20));
    }

    [Fact]
    public void StoppingTheEngineReleasesAndReportsBeforeTheWorkerExits()
    {
        using var harness = new EngineHarness();
        var seen = new Recorder();
        using var capture = harness.Host.CaptureKeys(seen.Add, Long);
        Assert.True(harness.Host.IsRunning);

        harness.Host.Stop();

        Assert.False(harness.Host.IsRunning);
        Assert.False(harness.Host.IsCapturingKeys);
        Assert.Equal([KeyCaptureEvent.Released(KeyCaptureEvent.EngineStoppedReason)], seen.Events);
    }

    [Fact]
    public void AThrowingCallbackIsLoggedAndTheCaptureHolds()
    {
        using var harness = new EngineHarness();
        using var capture = harness.Host.CaptureKeys(_ => throw new InvalidOperationException("boom"), Long);

        Assert.True(Key(harness, down: true, KeyCode.F5, 0));
        harness.WaitForLog(LogSources.Engine, "Key capture callback threw");

        Assert.True(harness.Host.IsCapturingKeys);
        Assert.True(Key(harness, down: false, KeyCode.F5, 10));
    }

    [Fact]
    public void ArmingRefusesANullCallbackAndANonPositiveTimeout()
    {
        using var harness = new EngineHarness();

        Assert.Throws<ArgumentNullException>(() => harness.Host.CaptureKeys(null!, Long));
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Host.CaptureKeys(_ => { }, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Host.CaptureKeys(_ => { }, Timeout.InfiniteTimeSpan));
        Assert.False(harness.Host.IsCapturingKeys);
        Assert.Equal(TimeSpan.FromSeconds(10), EngineHost.DefaultKeyCaptureIdleTimeout);
    }

    private static bool Key(EngineHarness harness, bool down, KeyCode key, long t, KeyModifiers modifiers = KeyModifiers.None)
        => harness.Source.Deliver(down ? RawInput.KeyDown(key, t, modifiers) : RawInput.KeyUp(key, t, modifiers));

    /// <summary>The worker handles messages in order: once a press observed after this call is reported, everything queued before it has been handled.</summary>
    private static void Barrier(EngineHarness harness)
    {
        var observed = 0;
        using var observation = harness.Host.CaptureNextButtonPress(_ => Interlocked.Increment(ref observed));
        harness.Down(MouseButton.X2, 0, 0, 0);
        harness.Up(MouseButton.X2, 0, 0, 0);
        EngineHarness.WaitFor(() => Volatile.Read(ref observed) == 1, "the barrier press");
    }

    private sealed class Recorder
    {
        private readonly object _gate = new();
        private readonly List<KeyCaptureEvent> _events = [];

        public IReadOnlyList<KeyCaptureEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return [.. _events];
                }
            }
        }

        public void Add(KeyCaptureEvent captured)
        {
            lock (_gate)
            {
                _events.Add(captured);
            }
        }

        public void WaitFor(int count) => EngineHarness.WaitFor(() => Events.Count >= count, $"{count} key capture events (have {Events.Count})");
    }
}
