using Augram.Core.Capture;
using Augram.Core.Config;

namespace Augram.Core.Abstractions;

/// <summary>
/// A native window the trail is drawn in, for a platform where the UI toolkit's own window cannot do it (F6; ADR-0002 §2
/// "<c>IOverlay</c> … with Platform fallback"). macOS: an Avalonia window never shows over another app's full-screen Space,
/// a non-activating panel does. The App keeps the rules (verify before show, park between strokes, the idle watchdog,
/// <c>Overlay/NativeTrailOverlay</c>); the surface only does what it is told. UI thread only. Creating the implementation
/// touches nothing native: the window is made by the first <see cref="Verify"/>, so a run that never strokes never makes one.
/// Coordinates are capture units (the hook's pointer positions): global top-left points on macOS.
/// </summary>
public interface ITrailSurface : IDisposable
{
    /// <summary>Creates the window on first use (never shown here), applies click-through, no-activate and the level, and reads them back.</summary>
    OverlayStyleReport Verify();

    /// <summary>Sizes the window to the display under <paramref name="strokeStart"/> without showing or activating it; returns the area covered.</summary>
    TrailSurfaceArea Cover(CapturePoint strokeStart);

    /// <summary>Shrinks the window to one unit at the main display's origin; it stays ordered in, covering nothing.</summary>
    void Park();

    /// <summary>Orders the window in front without activating Augram.</summary>
    void Show();

    void Hide();

    /// <summary>The colour, opacity and width the next points are drawn with.</summary>
    void SetStyle(TrailSettings style);

    /// <summary>The whole stroke so far in global capture units, relative to nothing; fewer than two points draws nothing.</summary>
    void SetPoints(IReadOnlyList<CapturePoint> points);
}

/// <summary>A rectangle in global capture units: what <see cref="ITrailSurface.Cover"/> spans, for the log.</summary>
public readonly record struct TrailSurfaceArea(double X, double Y, double Width, double Height);
