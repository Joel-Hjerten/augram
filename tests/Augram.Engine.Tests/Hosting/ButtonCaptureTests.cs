using Augram.Core.Capture;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>Detect-to-assign (F1): the next physical press is reported once, on the worker, and handled as usual.</summary>
public sealed class ButtonCaptureTests
{
    [Fact]
    public void ReportsTheNextPressOnce_WithoutChangingTheSuppressDecision()
    {
        using var harness = new EngineHarness();
        var seen = new List<MouseButton>();
        using var capture = harness.Host.CaptureNextButtonPress(seen.Add);

        Assert.False(harness.Down(MouseButton.X1, 10, 10, 0));
        Assert.False(harness.Up(MouseButton.X1, 10, 10, 10));
        EngineHarness.WaitFor(() => seen.Count == 1, "the observed press");
        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 20));
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 30));
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.Equal([MouseButton.X1], seen);
    }

    [Fact]
    public void TheStrokeButtonIsObservedAndStillCaptured()
    {
        using var harness = new EngineHarness();
        MouseButton? seen = null;
        using var capture = harness.Host.CaptureNextButtonPress(button => seen = button);

        Assert.True(harness.Down(EngineHarness.StrokeButton, 10, 10, 0));
        EngineHarness.WaitFor(() => seen is not null, "the observed press");
        Assert.True(harness.Up(EngineHarness.StrokeButton, 10, 10, 10));

        Assert.Equal(EngineHarness.StrokeButton, seen);
        EngineHarness.WaitFor(() => harness.Simulator.Clicks.Count == 1, "the replayed click");
    }

    [Fact]
    public void DisposingCancels_AndALaterCallReplaces()
    {
        using var harness = new EngineHarness();
        var first = new List<MouseButton>();
        var second = new List<MouseButton>();

        harness.Host.CaptureNextButtonPress(first.Add).Dispose();
        harness.Down(MouseButton.X2, 10, 10, 0);
        harness.Up(MouseButton.X2, 10, 10, 10);

        harness.Host.CaptureNextButtonPress(first.Add);
        using var replacement = harness.Host.CaptureNextButtonPress(second.Add);
        harness.Down(MouseButton.Middle, 10, 10, 20);
        harness.Up(MouseButton.Middle, 10, 10, 30);
        EngineHarness.WaitFor(() => second.Count == 1, "the replacement's press");

        Assert.Empty(first);
        Assert.Equal([MouseButton.Middle], second);
    }
}
