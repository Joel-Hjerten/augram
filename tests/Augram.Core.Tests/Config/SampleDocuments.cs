using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;

namespace Augram.Core.Tests.Config;

/// <summary>Documents with every setting off its default and awkward point values.</summary>
internal static class SampleDocuments
{
    public static Settings NonDefaultSettings { get; } = new()
    {
        General = new GeneralSettings(MouseButton.Middle, IgnoreKeys.Control | IgnoreKeys.Alt, StartAtLogin: true, Enabled: false, ColourMenuBarIcon: true),
        Capture = new CaptureThresholds(StartDistancePx: 12, MinSegmentPx: 3, CancelDelayMs: 1500, ResetCancelDelayOnMovement: false),
        Trail = new TrailSettings { WidthPx = 3.5, Opacity = 0.25, Colour = new RgbColor(10, 20, 30) },
        Recognition = new RecognitionOptions(Precision: 64, Threshold: 80.5, ScoringMode.Corrected, SampleAggregation.Best),
        NoMatch = NoMatchBehaviour.ReplayClick,
    };

    /// <summary>Three gestures, two samples each; the third is inactive.</summary>
    public static IReadOnlyList<Gesture> ThreeGestures { get; } =
    [
        TestGestures.Create("Alpha", [P(0, 0), P(12.345, -0.1), P(100.5, 1e-7)], [P(1, 2), P(3, 4)]),
        TestGestures.Create("Beta", [P(-50, 50), P(0, 0)], [P(0.1, 0.2), P(0.3, 0.4), P(0.5, 0.6)]),
        TestGestures.Create("Gamma", isActive: false, [P(1e6, -1e6), P(2, 2)], [P(7, 7), P(8, 9)]),
    ];

    public static ConfigDocument Full() => new() { Settings = NonDefaultSettings, Gestures = ThreeGestures };

    public static ConfigDocument WithGesture(string name)
        => new() { Gestures = [TestGestures.Create(name, [P(0, 0), P(100, 0)])] };

    private static GesturePoint P(double x, double y) => new(x, y);
}
