using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.Core.Capture;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>Detect-to-assign (F1): the next physical press becomes the stroke button, or the flow times out.</summary>
public sealed class StrokeButtonDetectionTests
{
    [Fact]
    public void TheNextPressBecomesTheStrokeButton()
    {
        using var engine = new EngineFixture();
        using var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action());

        detection.Start();
        Assert.True(detection.IsListening);
        Assert.Equal(StrokeButtonDetection.ListeningText, detection.Status);

        engine.Press(MouseButton.X2);
        // The worker clears IsListening before it writes Status, so wait for the status text itself.
        EngineFixture.WaitFor(() => detection.Status != StrokeButtonDetection.ListeningText, "the detection to finish");

        Assert.Equal(MouseButton.X2, engine.Settings.Current.General.StrokeButton);
        Assert.Equal("Stroke button set to X2.", detection.Status);
        Assert.True(engine.Settings.CanUndo);
    }

    [Fact]
    public void TimesOutWhenNothingIsPressed()
    {
        using var engine = new EngineFixture();
        using var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action(), TimeSpan.FromMilliseconds(50));

        detection.Start();
        EngineFixture.WaitFor(() => !detection.IsListening, "the timeout");

        Assert.Equal(StrokeButtonDetection.TimedOutText, detection.Status);
        Assert.Equal(MouseButton.Right, engine.Settings.Current.General.StrokeButton);

        // A press after the timeout changes nothing.
        engine.Press(MouseButton.Left, 100);
        EngineFixture.WaitFor(() => engine.Simulator.Clicks.Count == 0 && engine.Host.State == CaptureState.Idle, "the engine to settle");
        Assert.Equal(MouseButton.Right, engine.Settings.Current.General.StrokeButton);
    }

    [Fact]
    public void PressingTheCurrentButtonIsStillACaptureAndAReplay()
    {
        using var engine = new EngineFixture();
        using var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action());

        detection.Start();
        engine.Press(MouseButton.Right);
        // The worker clears IsListening before it writes Status, so wait for the status text itself.
        EngineFixture.WaitFor(() => detection.Status != StrokeButtonDetection.ListeningText, "the detection to finish");
        EngineFixture.WaitFor(() => engine.Simulator.Clicks.Count == 1, "the replayed click");

        Assert.Equal("Stroke button set to Right.", detection.Status);
        Assert.False(engine.Settings.CanUndo);
    }
}
