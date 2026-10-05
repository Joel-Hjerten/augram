namespace Augram.Core.Abstractions;

/// <summary>No native fix-up: the default for platforms without one and for headless tests. Reports <see cref="OverlayStyleReport.NotApplicable"/>.</summary>
public sealed class NullOverlayWindowStyle : IOverlayWindowStyle
{
    private NullOverlayWindowStyle()
    {
    }

    public static NullOverlayWindowStyle Instance { get; } = new();

    public OverlayStyleReport Apply(nint handle) => OverlayStyleReport.NotApplicable;

    public void Place(nint handle, int x, int y, int width, int height)
    {
    }
}
