using Augram.App.Overlay;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Overlay;

/// <summary>
/// The overlay window over the headless platform: hidden when idle, shown by a stroke's first frame once the
/// style is verified, hidden by its last frame; trail calls from a background thread are one coalesced frame each.
/// </summary>
public sealed class TrailOverlayWindowTests
{
    [AvaloniaFact]
    public void ShownOnBegin_OneFramePerRequest_HiddenOnEnd()
    {
        var log = new ListEventLog();
        var style = new FakeOverlayStyle(OverlayStyleReport.NotApplicable);
        using var window = new TrailOverlayWindow(() => TrailSettings.Default, style, log, new HealthRegistry());
        Assert.False(window.IsVisible);
        Assert.False(window.ShowInTaskbar);
        Assert.False(window.IsHitTestVisible);
        Assert.True(window.Topmost);

        OnWorker(() =>
        {
            window.Begin(new CapturePoint(100, 100, 0));
            for (var i = 1; i <= 40; i++)
            {
                window.Extend(new CapturePoint(100 + (i * 5), 100, i * 10));
            }
        });
        Assert.Equal(1, window.Buffer.RequestCount);
        Assert.Equal(0, window.FramesRendered);
        Assert.False(window.IsVisible);

        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsVisible);
        Assert.True(window.IsUsable);
        Assert.True(style.ApplyCount >= 2, "styles are applied before Show and verified after it");
        Assert.True(log.Has(TrailOverlayWindow.LogSource, "Overlay style verified"));
        Assert.Equal(1, window.FramesRendered);
        Assert.Equal(41, window.PointsOnScreen);

        OnWorker(() =>
        {
            window.Extend(new CapturePoint(400, 100, 500));
            window.End();
        });
        Assert.Equal(2, window.Buffer.RequestCount);
        Assert.Equal(0, window.Buffer.PendingPointCount);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, window.FramesRendered);
        Assert.Equal(0, window.PointsOnScreen);
        // The window hides one frame after painting the empty canvas (no stale stroke on the next show), so pump a frame.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsVisible);
        Assert.DoesNotContain(log.Events, e => e.Level == EventLevel.Error);
    }

    [AvaloniaFact]
    public void AClickThatEndsBeforeTheFirstFrameNeverShowsTheWindow()
    {
        using var window = new TrailOverlayWindow(() => TrailSettings.Default, NullOverlayWindowStyle.Instance, NullEventLog.Instance, null);

        OnWorker(() =>
        {
            window.Begin(new CapturePoint(5, 5, 0));
            window.End();
        });
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
        Assert.Equal(1, window.FramesRendered);
    }

    [AvaloniaFact]
    public void NotClickThrough_StaysHidden_LogsError_AndTheTrailBecomesANoOp()
    {
        var log = new ListEventLog();
        var style = new FakeOverlayStyle(new OverlayStyleReport(false, true, true, "0x88"));
        using var window = new TrailOverlayWindow(() => TrailSettings.Default, style, log, null);

        OnWorker(() =>
        {
            window.Begin(new CapturePoint(100, 100, 0));
            window.Extend(new CapturePoint(150, 100, 10));
        });
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
        Assert.False(window.IsUsable);
        Assert.Equal(0, window.PointsOnScreen);
        Assert.True(log.Has(TrailOverlayWindow.LogSource, "Overlay not click-through, hidden"));
        Assert.Contains(log.Events, e => e.Level == EventLevel.Error && e.Source == TrailOverlayWindow.LogSource);

        var requests = window.Buffer.RequestCount;
        OnWorker(() => window.Begin(new CapturePoint(1, 1, 20)));
        Assert.Equal(requests, window.Buffer.RequestCount);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void WatchdogHidesAWindowLeftVisibleAfterTheStrokeEnded()
    {
        var log = new ListEventLog();
        using var window = new TrailOverlayWindow(() => TrailSettings.Default, NullOverlayWindowStyle.Instance, log, null);
        OnWorker(() =>
        {
            window.Begin(new CapturePoint(100, 100, 0));
            window.Extend(new CapturePoint(150, 100, 10));
        });
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.IsVisible);

        // The stroke is still active: the watchdog leaves it alone however long it has been visible.
        window.CheckIdle(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));
        Assert.True(window.IsVisible);

        // Ended on the worker, but the hiding frame has not run: within the limit nothing, past it the watchdog hides and reports.
        OnWorker(window.End);
        window.CheckIdle(DateTimeOffset.UtcNow);
        Assert.True(window.IsVisible);
        window.CheckIdle(DateTimeOffset.UtcNow + TrailOverlayWindow.IdleVisibleLimit + TimeSpan.FromSeconds(1));

        Assert.False(window.IsVisible);
        Assert.True(log.Has(TrailOverlayWindow.LogSource, "Overlay visible without a stroke, hidden"));
    }

    [AvaloniaFact]
    public void TheStyleIsReadAtBegin()
    {
        var style = TrailSettings.Default;
        using var window = new TrailOverlayWindow(() => style, NullOverlayWindowStyle.Instance, NullEventLog.Instance, null);

        style = new TrailSettings { WidthPx = 12, Colour = new RgbColor(9, 8, 7), Opacity = 1 };
        OnWorker(() => window.Begin(new CapturePoint(5, 5, 0)));

        Assert.Same(style, window.Buffer.Take().Style);
    }

    private static void OnWorker(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = e;
            }
        })
        { Name = "test-worker" };
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
