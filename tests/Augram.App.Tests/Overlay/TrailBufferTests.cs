using Augram.App.Overlay;
using Augram.Core.Capture;
using Augram.Core.Config;
using Xunit;

namespace Augram.App.Tests.Overlay;

/// <summary>The worker-to-UI hand-off: one render request per taken frame, points cleared on End.</summary>
public sealed class TrailBufferTests
{
    [Fact]
    public void ABurstOfPointsIsOneRequestUntilTheFrameIsTaken()
    {
        var requests = 0;
        var buffer = new TrailBuffer(() => requests++);

        buffer.Begin(new CapturePoint(10, 10, 0), TrailSettings.Default);
        for (var i = 1; i <= 50; i++)
        {
            buffer.Extend(new CapturePoint(10 + i, 10, i));
        }

        Assert.Equal(1, requests);
        var frame = buffer.Take();
        Assert.True(frame.IsStrokeStart);
        Assert.True(frame.IsActive);
        Assert.Equal(51, frame.Points.Count);

        buffer.Extend(new CapturePoint(99, 10, 60));
        Assert.Equal(2, requests);
        var next = buffer.Take();
        Assert.False(next.IsStrokeStart);
        Assert.Equal(52, next.Points.Count);
    }

    [Fact]
    public void EndClearsThePointsAndRequestsAClearingFrame()
    {
        var requests = 0;
        var buffer = new TrailBuffer(() => requests++);
        buffer.Begin(new CapturePoint(10, 10, 0), TrailSettings.Default);
        buffer.Extend(new CapturePoint(20, 10, 1));
        buffer.Take();

        buffer.End();

        Assert.Equal(2, requests);
        Assert.Equal(0, buffer.PendingPointCount);
        var frame = buffer.Take();
        Assert.False(frame.IsActive);
        Assert.Empty(frame.Points);

        buffer.Extend(new CapturePoint(30, 10, 2));
        Assert.Equal(0, buffer.PendingPointCount);
        Assert.Equal(2, requests);
    }

    [Fact]
    public void BeginCarriesTheStyleReadAtThatMoment()
    {
        var buffer = new TrailBuffer(() => { });
        var style = new TrailSettings { WidthPx = 9, Opacity = 0.25, Colour = new RgbColor(1, 2, 3) };

        buffer.Begin(new CapturePoint(0, 0, 0), style);

        Assert.Same(style, buffer.Take().Style);
    }

    [Fact]
    public void ConcurrentExtendsFromAnotherThreadLoseNothing()
    {
        var requests = 0;
        var buffer = new TrailBuffer(() => Interlocked.Increment(ref requests));
        buffer.Begin(new CapturePoint(0, 0, 0), TrailSettings.Default);
        var thread = new Thread(() =>
        {
            for (var i = 1; i <= 1000; i++)
            {
                buffer.Extend(new CapturePoint(i, 0, i));
            }
        });

        thread.Start();
        thread.Join();

        Assert.Equal(1, requests);
        Assert.Equal(1001, buffer.Take().Points.Count);
    }
}
