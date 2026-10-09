using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Gestures.Cleanup;
using Xunit;

namespace Augram.Core.Tests.Gestures;

/// <summary>Shape cleanup on a whole gesture: the drawn samples kept beside the cleaned ones, restorable, and stored with the gesture.</summary>
public sealed class GestureCleanupTests
{
    private static readonly GestureSample Drawn = new([new(0, 0), new(30, 1.5), new(60, -1), new(90, 2), new(120, 0)]);

    private static Gesture AsDrawn => new(GestureId.New(), "Right", true, [Drawn]);

    [Fact]
    public void CleanUpKeepsTheDrawnSamples_AndRestorePutsThemBack()
    {
        var cleaned = GestureCleanup.CleanUp(AsDrawn);

        Assert.True(cleaned.IsCleanedUp);
        Assert.Same(Drawn, Assert.Single(cleaned.OriginalSamples!));
        Assert.Equal(2, Assert.Single(cleaned.Samples).Count);

        var restored = GestureCleanup.Restore(cleaned);
        Assert.False(restored.IsCleanedUp);
        Assert.Same(Drawn, Assert.Single(restored.Samples));
    }

    [Fact]
    public void CleaningAgainStartsFromTheDrawnSamples()
    {
        var twice = GestureCleanup.CleanUp(GestureCleanup.CleanUp(AsDrawn));

        Assert.Same(Drawn, Assert.Single(twice.OriginalSamples!));
    }

    [Fact]
    public void AStrokeIsStoredCleanedWithItsOriginal_OrAsDrawn()
    {
        var stroke = Drawn.ToList();

        Assert.True(GestureCleanup.FromStroke(AsDrawn, stroke, cleanUp: true).IsCleanedUp);
        var asDrawn = GestureCleanup.FromStroke(GestureCleanup.CleanUp(AsDrawn), stroke, cleanUp: false);
        Assert.False(asDrawn.IsCleanedUp);
        Assert.Equal(stroke, Assert.Single(asDrawn.Samples));
    }

    [Fact]
    public void TheOriginalIsWrittenOnlyWhenThereIsOne_AndReadBack()
    {
        var plain = ConfigSerializer.Write(new ConfigDocument { Gestures = [AsDrawn] });
        var cleaned = ConfigSerializer.Write(new ConfigDocument { Gestures = [GestureCleanup.CleanUp(AsDrawn)] });

        Assert.DoesNotContain("originalSamples", plain, StringComparison.Ordinal);
        Assert.Contains("\"originalSamples\"", cleaned, StringComparison.Ordinal);
        var back = Assert.Single(ConfigSerializer.Read(cleaned).Gestures);
        Assert.Equal(Drawn.ToList(), Assert.Single(back.OriginalSamples!).ToList());
        Assert.Null(Assert.Single(ConfigSerializer.Read(plain).Gestures).OriginalSamples);
    }
}
