using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>The engine's knobs and lifecycle: the tray toggle, ignore key, live setting changes, hook reinstall, start/stop/dispose.</summary>
public sealed class EngineHostControlTests
{
    [Fact]
    public void Disabled_PassesEverythingThrough_AndLogsTheToggle()
    {
        using var harness = new EngineHarness();
        harness.Host.Enabled = false;

        var (down, up) = harness.Stroke(200, 0);

        Assert.False(down);
        Assert.False(up);
        harness.WaitForState(CaptureState.Idle);
        Assert.True(harness.Log.Has(LogSources.Engine, "Engine disabled"));
        Assert.Empty(harness.Events);
        Assert.Empty(harness.Simulator.Clicks);
        Assert.Empty(harness.Trail.Calls);

        harness.Host.Enabled = true;
        Assert.True(harness.Log.Has(LogSources.Engine, "Engine enabled"));
        Assert.True(harness.Stroke(200, 0, startMs: 1000).Down);
        harness.WaitForEvents(1);
    }

    [Fact]
    public void DisabledMidCapture_StillConsumesTheOwedRelease()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        harness.Host.Enabled = false;
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 50));

        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
        Assert.False(harness.Down(EngineHarness.StrokeButton, 10, 10, 100));
        Assert.False(harness.Up(EngineHarness.StrokeButton, 10, 10, 150));
    }

    [Fact]
    public void IgnoreKeyHeld_PassesThePressThrough()
    {
        using var harness = new EngineHarness(new EngineHostOptions(EngineHarness.StrokeButton, IgnoreKey: KeyModifiers.Control, HealthPollInterval: TimeSpan.FromHours(1)));

        Assert.False(harness.Down(EngineHarness.StrokeButton, 10, 10, 0, KeyModifiers.Control));
        Assert.False(harness.Up(EngineHarness.StrokeButton, 10, 10, 10));
        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 20, KeyModifiers.Shift));
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 30));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        harness.Host.IgnoreKey = KeyModifiers.Shift;
        Assert.False(harness.Down(EngineHarness.StrokeButton, 10, 10, 40, KeyModifiers.Shift));
        Assert.False(harness.Up(EngineHarness.StrokeButton, 10, 10, 50));
    }

    [Fact]
    public void StrokeButtonChange_IsAppliedInOrder_AndLogged()
    {
        using var harness = new EngineHarness();

        harness.Host.StrokeButton = MouseButton.Middle;
        EngineHarness.WaitFor(() => harness.Host.StrokeButton == MouseButton.Middle, "the button change to apply");

        Assert.False(harness.Down(MouseButton.Right, 10, 10, 0));
        Assert.False(harness.Up(MouseButton.Right, 10, 10, 10));
        Assert.True(harness.Down(MouseButton.Middle, 10, 10, 20));
        Assert.True(harness.Up(MouseButton.Middle, 10, 10, 30));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
        Assert.Equal(MouseButton.Middle, harness.Simulator.Clicks[0].Button);
        Assert.True(harness.Log.Has(LogSources.Engine, "Stroke button changed"));
        Assert.DoesNotContain(harness.Log.Events, e => e.Message == "Suppression decision mismatch");
    }

    [Fact]
    public void ThresholdsChange_TakesEffect()
    {
        using var harness = new EngineHarness();
        harness.Host.SetThresholds(new CaptureThresholds(StartDistancePx: 500));
        EngineHarness.WaitFor(() => harness.Log.Has(LogSources.Engine, "Capture thresholds changed"), "the thresholds change to apply");

        harness.Stroke(200, 0);

        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
        Assert.Empty(harness.Trail.Calls);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public void HookReinstall_ResetsTheCapture()
    {
        using var harness = new EngineHarness();

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        harness.WaitForState(CaptureState.Held);
        harness.Source.ReportLost();
        harness.Host.Health.Poll();

        harness.WaitForState(CaptureState.Idle);
        Assert.Equal(1, harness.Host.Health.ReinstallCount);
        Assert.True(harness.Log.Has(LogSources.Capture, "Capture reset"));
        Assert.False(harness.Up(EngineHarness.StrokeButton, 10, 10, 100), "the release after a reinstall must pass through (nothing is owed)");
        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 200));
    }

    [Fact]
    public void StartStopDispose_AreLogged_AndIdempotent()
    {
        var harness = new EngineHarness();
        Assert.True(harness.Log.Has(LogSources.Engine, "Engine starting"));
        Assert.Throws<InvalidOperationException>(harness.Host.Start);

        harness.Host.Stop();
        Assert.True(harness.Log.Has(LogSources.Engine, "Engine stopped"));
        Assert.False(harness.Source.IsRunning);
        harness.Dispose();
        harness.Dispose();
        Assert.Throws<ObjectDisposedException>(harness.Host.Start);
    }
}
