using Augram.Core.Capture;
using Augram.Core.Config;

namespace Augram.App.Overlay;

/// <summary>
/// One UI-thread turn's view of the trail, taken from <see cref="TrailBuffer"/>: the whole stroke so far
/// in physical pixels, the look to draw it with (re-read from settings at Begin), whether this frame
/// starts a new stroke (and when it began, in <see cref="System.Diagnostics.Stopwatch"/> ticks, for the
/// first-frame latency), and whether the stroke is still being drawn.
/// </summary>
public sealed record TrailFrame(
    IReadOnlyList<CapturePoint> Points,
    TrailSettings Style,
    bool IsStrokeStart,
    long StrokeStartedTicks,
    bool IsActive);
