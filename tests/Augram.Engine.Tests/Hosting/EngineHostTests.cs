using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>
/// The capture paths end to end through the ports with fakes: scripted raw input in, hook decisions out
/// synchronously, recognition log entries, engine events, replayed clicks and trail calls out of the worker.
/// Settings, toggles and lifecycle are in <see cref="EngineHostControlTests"/>.
/// </summary>
public sealed class EngineHostTests
{
    [Fact]
    public void Stroke_IsRecognized_LoggedAndRaised()
    {
        using var harness = new EngineHarness();

        var (down, up) = harness.Stroke(200, 0);

        Assert.True(down);
        Assert.True(up);
        harness.WaitForEvents(1);
        var recognized = Assert.IsType<EngineEvent.GestureRecognized>(harness.Events[0]);
        Assert.Equal("right", recognized.Name);
        Assert.True(recognized.Score > 75, $"score {recognized.Score}");
        Assert.Equal(new CapturePoint(100, 100, 0), recognized.Start);
        Assert.Equal(21, recognized.Points.Count);

        var entry = Assert.Single(harness.RecognitionLog.Snapshot());
        Assert.Equal(21, entry.PointCount);
        Assert.Equal(200, entry.DurationMs);
        Assert.Equal("right", Assert.Single(entry.TopMatches).Name);
        Assert.Equal(StrokeRecognizer.NoCommandReason, entry.NothingFiredReason);

        var line = harness.Log.Single(LogSources.Recognition, "Gesture recognized");
        Assert.Equal(EventLevel.Info, line.Level);
        Assert.Contains(line.Properties!, p => p.Key == "gesture" && (string)p.Value! == "right");
        Assert.Contains(line.Properties!, p => p.Key == "worstHandlerUs");
        // BeginStroke replays the points recorded before the start distance was reached, so every move becomes an extend.
        Assert.Equal(["begin", .. Enumerable.Repeat("extend", 20), "end"], harness.Trail.Calls);
        Assert.Empty(harness.Simulator.Clicks);
        Assert.NotNull(harness.Health.Current().LastStrokeLatencyMs);
        harness.WaitForState(CaptureState.Idle);
    }

    [Fact]
    public void Stroke_MatchingNothing_RaisesNoMatchWithTheReason()
    {
        using var harness = new EngineHarness();

        harness.Stroke(0, 200);

        harness.WaitForEvents(1);
        var noMatch = Assert.IsType<EngineEvent.NoMatch>(harness.Events[0]);
        Assert.StartsWith("best score", noMatch.Reason, StringComparison.Ordinal);
        Assert.Equal(noMatch.Reason, Assert.Single(harness.RecognitionLog.Snapshot()).NothingFiredReason);
        Assert.True(harness.Log.Has(LogSources.Recognition, "No match"));
    }

    [Fact]
    public void Stroke_WithNoGestures_SaysSo()
    {
        using var harness = new EngineHarness(gestures: []);

        harness.Stroke(200, 0);

        harness.WaitForEvents(1);
        Assert.Equal(StrokeRecognizer.NoGesturesReason, Assert.IsType<EngineEvent.NoMatch>(harness.Events[0]).Reason);
    }

    [Fact]
    public void MotionlessPress_IsReplayedAsAClick_ByTheWorker()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 50, 60, 0));
        harness.Move(52, 61, 5);
        Assert.True(harness.Up(EngineHarness.StrokeButton, 52, 61, 80));

        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
        Assert.Equal((MouseButton.Right, 52, 61), harness.Simulator.Clicks[0]);
        Assert.True(harness.Log.Has(LogSources.Capture, "Click replayed"));
        Assert.Empty(harness.Events);
        Assert.Empty(harness.Trail.Calls);
        Assert.Empty(harness.RecognitionLog.Snapshot());
    }

    [Fact]
    public void WheelWhileHeld_IsSuppressedAndRaised_EveryTick_ReleaseFiresNothing()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        harness.WaitForState(CaptureState.Held);
        Assert.True(harness.Wheel(WheelDirection.Up, 10, 10, 20));
        harness.WaitForState(CaptureState.WheelFiring);
        Assert.True(harness.Wheel(WheelDirection.Down, 10, 10, 40));
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 60));

        harness.WaitForEvents(2);
        Assert.Equal(WheelDirection.Up, Assert.IsType<EngineEvent.WheelTriggered>(harness.Events[0]).Direction);
        Assert.Equal(WheelDirection.Down, Assert.IsType<EngineEvent.WheelTriggered>(harness.Events[1]).Direction);
        harness.WaitForState(CaptureState.Idle);
        Assert.Empty(harness.Simulator.Clicks);
        Assert.Empty(harness.RecognitionLog.Snapshot());
        Assert.Equal(2, harness.Log.Events.Count(e => e.Source == LogSources.Capture && e.Message == "Wheel trigger"));
    }

    [Fact]
    public void WheelWhileIdle_PassesThrough()
    {
        using var harness = new EngineHarness();

        Assert.False(harness.Wheel(WheelDirection.Up, 10, 10, 0));
        Assert.False(harness.Wheel(WheelDirection.Down, 10, 10, 10));
    }

    [Fact]
    public void OtherButtonWhileDrawing_CancelsEndsTheTrail_AndConsumesTheRelease()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 100, 100, 0));
        harness.Move(150, 100, 10);
        harness.Move(200, 100, 20);
        EngineHarness.WaitFor(() => harness.Trail.BeginCount == 1, "the trail to begin");
        Assert.False(harness.Down(MouseButton.Left, 200, 100, 30));
        harness.WaitForState(CaptureState.Cancelled);
        Assert.False(harness.Up(MouseButton.Left, 200, 100, 40));
        Assert.True(harness.Up(EngineHarness.StrokeButton, 200, 100, 50));

        harness.WaitForState(CaptureState.Idle);
        EngineHarness.WaitFor(() => harness.Trail.EndCount == 1, "the trail to end");
        Assert.True(harness.Log.Has(LogSources.Capture, "Gesture cancelled"));
        Assert.Empty(harness.Events);
        Assert.Empty(harness.Simulator.Clicks);
    }

    [Fact]
    public void HoldingStill_CancelsThroughTheTickTimer()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 100, 100, 0));
        harness.WaitForState(CaptureState.Held);
        harness.Clock.MonotonicMs = CaptureThresholds.Default.CancelDelayMs + 10;

        harness.WaitForState(CaptureState.Cancelled);
        Assert.True(harness.Up(EngineHarness.StrokeButton, 100, 100, harness.Clock.MonotonicMs));
        harness.WaitForState(CaptureState.Idle);
        Assert.Empty(harness.Simulator.Clicks);
        Assert.Empty(harness.Events);
        var cancelled = harness.Log.Single(LogSources.Capture, "Gesture cancelled");
        Assert.Contains(cancelled.Properties!, p => p.Key == "reason" && (CancelReason)p.Value! == CancelReason.HoldStill);
    }

    [Fact]
    public void NoDecisionMismatches_OverARandomSession()
    {
        using var harness = new EngineHarness();
        var rng = new Random(7);
        var down = false;
        long t = 0;
        for (var i = 0; i < 500; i++)
        {
            t += rng.Next(1, 30);
            switch (rng.Next(5))
            {
                case 0 when !down:
                    down = true;
                    harness.Down(EngineHarness.StrokeButton, rng.Next(500), rng.Next(500), t);
                    break;
                case 1 when down:
                    down = false;
                    harness.Up(EngineHarness.StrokeButton, rng.Next(500), rng.Next(500), t);
                    break;
                case 2:
                    harness.Move(rng.Next(500), rng.Next(500), t);
                    break;
                case 3:
                    harness.Wheel(WheelDirection.Up, rng.Next(500), rng.Next(500), t);
                    break;
                default:
                    harness.Down(MouseButton.Left, 0, 0, t);
                    harness.Up(MouseButton.Left, 0, 0, t);
                    break;
            }
        }

        if (down)
        {
            harness.Up(EngineHarness.StrokeButton, 0, 0, t + 1);
        }

        harness.WaitForState(CaptureState.Idle);
        harness.Host.Stop();
        Assert.DoesNotContain(harness.Log.Events, e => e.Message == "Suppression decision mismatch" && e.Level == EventLevel.Warning);
        Assert.Equal(0, harness.Host.DroppedMoveCount);
    }
}
