using Augram.App.Overlay;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Overlay;

/// <summary>
/// The trail over a platform's own surface (the macOS panel): nothing native until a stroke, verified before every stroke
/// covers a display, drawn before it is ordered in, parked between strokes, hidden for good when not click-through, and
/// hidden by the watchdog when left full-size.
/// </summary>
public sealed class NativeTrailOverlayTests
{
    [AvaloniaFact]
    public void NothingNativeHappensUntilAStroke()
    {
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, NullEventLog.Instance, null);

        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Calls);
        Assert.False(overlay.IsVisible);
    }

    [AvaloniaFact]
    public void AStrokeIsVerifiedCoveredAndDrawnBeforeItIsShown_ThenParkedOnEnd()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        var health = new HealthRegistry();
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, health);

        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(100, 100, 0));
            for (var i = 1; i <= 40; i++)
            {
                overlay.Extend(new CapturePoint(100 + (i * 5), 100, i * 10));
            }
        });
        Assert.Equal(1, overlay.Buffer.RequestCount);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Verify", "SetStyle", "Cover", "SetPoints(41)", "Show"], surface.Calls);
        Assert.True(overlay.IsVisible);
        Assert.False(overlay.IsParked);
        Assert.Same(TrailSettings.Default, surface.Style);
        Assert.True(log.Has(NativeTrailOverlay.LogSource, "Overlay style verified"));
        Assert.True(log.Has(NativeTrailOverlay.LogSource, "Overlay area"));
        Assert.True(log.Has(NativeTrailOverlay.LogSource, "Trail first frame"));

        OnWorker(() =>
        {
            overlay.Extend(new CapturePoint(400, 100, 500));
            overlay.End();
        });
        Dispatcher.UIThread.RunJobs();

        // Cleared and shrunk, never hidden: no stale stroke can flash on the next one.
        Assert.Equal(["SetPoints(0)", "Park"], surface.Calls[^2..]);
        Assert.True(overlay.IsVisible);
        Assert.True(overlay.IsParked);
        Assert.True(surface.IsOrderedIn);
        Assert.Empty(surface.Points);
        Assert.DoesNotContain(log.Events, e => e.Level == EventLevel.Error);
    }

    [AvaloniaFact]
    public void TheNextStrokeIsVerifiedAgainAndGrowsTheParkedSurfaceWithoutAnotherShow()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, null);
        Stroke(overlay);
        surface.Calls.Clear();

        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(10, 10, 0));
            overlay.Extend(new CapturePoint(20, 10, 10));
        });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Verify", "SetStyle", "Cover", "SetPoints(2)"], surface.Calls);
        Assert.False(overlay.IsParked);
        Assert.Single(log.Events, e => e.Message == "Overlay style verified");
    }

    [AvaloniaFact]
    public void AClickThatEndsBeforeTheFirstFrameTouchesNothing()
    {
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, NullEventLog.Instance, null);

        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(5, 5, 0));
            overlay.End();
        });
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(surface.Calls);
        Assert.False(overlay.IsVisible);
        Assert.Equal(1, overlay.FramesRendered);
    }

    [AvaloniaFact]
    public void NotClickThrough_NeverCovers_LogsError_AndTheTrailBecomesANoOp()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(new OverlayStyleReport(false, true, true, "panel ignoresMouseEvents=0"));
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, null);

        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(100, 100, 0));
            overlay.Extend(new CapturePoint(150, 100, 10));
        });
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("Cover", surface.Calls);
        Assert.DoesNotContain("Show", surface.Calls);
        Assert.False(surface.IsOrderedIn);
        Assert.False(overlay.IsVisible);
        Assert.False(overlay.IsUsable);
        Assert.Contains(log.Events, e => e.Level == EventLevel.Error && e.Message == "Overlay not click-through, hidden");

        var requests = overlay.Buffer.RequestCount;
        OnWorker(() => overlay.Begin(new CapturePoint(1, 1, 20)));
        Assert.Equal(requests, overlay.Buffer.RequestCount);
    }

    [AvaloniaFact]
    public void ASurfaceThatLosesClickThroughWhileParkedIsHiddenAtTheNextStroke()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, null);
        Stroke(overlay);
        Assert.True(overlay.IsParked);

        surface.Report = new OverlayStyleReport(false, true, true, "panel ignoresMouseEvents=0");
        surface.Calls.Clear();
        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(10, 10, 0));
            overlay.Extend(new CapturePoint(20, 10, 10));
        });
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("Cover", surface.Calls);
        Assert.Contains("Hide", surface.Calls);
        Assert.False(surface.IsOrderedIn);
        Assert.False(overlay.IsVisible);
        Assert.False(overlay.IsUsable);
    }

    [AvaloniaFact]
    public void WatchdogHidesASurfaceLeftFullSizeAfterTheStrokeEnded()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, null);
        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(100, 100, 0));
            overlay.Extend(new CapturePoint(150, 100, 10));
        });
        Dispatcher.UIThread.RunJobs();

        // Still active: left alone however long.
        overlay.CheckIdle(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));
        Assert.True(surface.IsOrderedIn);

        // Ended on the worker, the parking frame not run yet: within the limit nothing, past it hidden and reported.
        OnWorker(overlay.End);
        overlay.CheckIdle(DateTimeOffset.UtcNow);
        Assert.True(surface.IsOrderedIn);
        overlay.CheckIdle(DateTimeOffset.UtcNow + TrailOverlayWindow.IdleVisibleLimit + TimeSpan.FromSeconds(1));

        Assert.False(surface.IsOrderedIn);
        Assert.False(overlay.IsVisible);
        Assert.True(log.Has(NativeTrailOverlay.LogSource, "Overlay visible without a stroke, hidden"));
    }

    [AvaloniaFact]
    public void AParkedSurfaceIsLeftAloneByTheWatchdog()
    {
        var log = new ListEventLog();
        var surface = new FakeTrailSurface(OverlayStyleReport.NotApplicable);
        using var overlay = new NativeTrailOverlay(() => TrailSettings.Default, surface, log, null);
        Stroke(overlay);

        overlay.CheckIdle(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));

        Assert.True(overlay.IsParked);
        Assert.DoesNotContain(log.Events, e => e.Level == EventLevel.Error);
    }

    private static void Stroke(NativeTrailOverlay overlay)
    {
        OnWorker(() =>
        {
            overlay.Begin(new CapturePoint(100, 100, 0));
            overlay.Extend(new CapturePoint(150, 100, 10));
        });
        Dispatcher.UIThread.RunJobs();
        OnWorker(overlay.End);
        Dispatcher.UIThread.RunJobs();
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
