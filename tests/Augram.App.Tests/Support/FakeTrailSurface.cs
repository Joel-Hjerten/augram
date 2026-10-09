using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;

namespace Augram.App.Tests.Support;

/// <summary>An <see cref="ITrailSurface"/> that reports whatever the test says and records every call in order, so a test can check what came before a show.</summary>
internal sealed class FakeTrailSurface : ITrailSurface
{
    public FakeTrailSurface(OverlayStyleReport report)
    {
        Report = report;
    }

    public OverlayStyleReport Report { get; set; }

    public TrailSurfaceArea Area { get; set; } = new(0, 0, 1512, 982);

    public List<string> Calls { get; } = [];

    public bool IsOrderedIn { get; private set; }

    public bool IsParked { get; private set; }

    public IReadOnlyList<CapturePoint> Points { get; private set; } = [];

    public TrailSettings? Style { get; private set; }

    public bool IsDisposed { get; private set; }

    public OverlayStyleReport Verify()
    {
        Calls.Add("Verify");
        return Report;
    }

    public TrailSurfaceArea Cover(CapturePoint strokeStart)
    {
        Calls.Add("Cover");
        IsParked = false;
        return Area;
    }

    public void Park()
    {
        Calls.Add("Park");
        IsParked = true;
    }

    public void Show()
    {
        Calls.Add("Show");
        IsOrderedIn = true;
    }

    public void Hide()
    {
        Calls.Add("Hide");
        IsOrderedIn = false;
    }

    public void SetStyle(TrailSettings style)
    {
        Calls.Add("SetStyle");
        Style = style;
    }

    public void SetPoints(IReadOnlyList<CapturePoint> points)
    {
        Calls.Add($"SetPoints({points.Count})");
        Points = [.. points];
    }

    public void Dispose() => IsDisposed = true;
}
