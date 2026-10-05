using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>An <see cref="IOverlayWindowStyle"/> that reports whatever the test says and counts the applies.</summary>
internal sealed class FakeOverlayStyle : IOverlayWindowStyle
{
    public FakeOverlayStyle(OverlayStyleReport report)
    {
        Report = report;
    }

    public OverlayStyleReport Report { get; set; }

    public int ApplyCount { get; private set; }

    public nint LastHandle { get; private set; }

    public (int X, int Y, int Width, int Height)? Placed { get; private set; }

    public OverlayStyleReport Apply(nint handle)
    {
        ApplyCount++;
        LastHandle = handle;
        return Report;
    }

    public void Place(nint handle, int x, int y, int width, int height) => Placed = (x, y, width, height);
}
