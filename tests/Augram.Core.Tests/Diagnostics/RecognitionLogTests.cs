using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Xunit;

namespace Augram.Core.Tests.Diagnostics;

/// <summary>The recognition log store (A15): 200 strokes by default, newest kept, at most three candidates per entry.</summary>
public sealed class RecognitionLogTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 21, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void DefaultCapacity_Is200()
    {
        var log = new RecognitionLog();

        Assert.Equal(200, log.Capacity);
        Assert.Equal(RecognitionLog.DefaultCapacity, log.Capacity);
    }

    [Fact]
    public void KeepsTheLast200InOrder()
    {
        var log = new RecognitionLog();

        for (var i = 0; i < 250; i++)
        {
            log.Add(Entry(i));
        }

        var snapshot = log.Snapshot();
        Assert.Equal(200, snapshot.Count);
        Assert.Equal(50, snapshot[0].PointCount);
        Assert.Equal(249, snapshot[^1].PointCount);
        Assert.Equal(250, log.Version);
    }

    [Fact]
    public void Entry_TrimsTopMatchesToThreeKeepingTheBestFirst()
    {
        var entry = new RecognitionLogEntry(T0, 40, 300,
        [
            new("Up", 95),
            new("UpRight", 80),
            new("Right", 60),
            new("Down", 20),
            new("Left", 10),
        ]);

        Assert.Equal(["Up", "UpRight", "Right"], entry.TopMatches.Select(m => m.Name));
        Assert.Equal(RecognitionLogEntry.MaxTopMatches, entry.TopMatches.Count);
    }

    [Fact]
    public void Entry_KeepsThreeOrFewerUntouched()
    {
        RecognitionCandidate[] two = [new("Up", 95), new("Down", 10)];

        var entry = new RecognitionLogEntry(T0, 40, 300, two);

        Assert.Same(two, entry.TopMatches);
    }

    [Fact]
    public void Entry_RecordsOutcomeFields()
    {
        var fired = new RecognitionLogEntry(T0, 40, 300, [new("Up", 95)], MatchedGroup: "Chrome", FiredCommand: "New tab");
        var nothing = fired with { FiredCommand = null, NothingFiredReason = "below threshold (72 < 75)" };

        Assert.Equal("New tab", fired.FiredCommand);
        Assert.Null(fired.NothingFiredReason);
        Assert.Equal("below threshold (72 < 75)", nothing.NothingFiredReason);
        Assert.Equal("Chrome", nothing.MatchedGroup);
        Assert.Equal(fired.TopMatches, nothing.TopMatches);
    }

    [Fact]
    public void Entry_KeepsTheMatchedGestureAndTheStrokeForTheGlyphs()
    {
        var id = GestureId.New();
        GesturePoint[] stroke = [new(0, 0), new(10, 0), new(20, 5)];

        var matched = new RecognitionLogEntry(T0, 3, 120, [new("Right", 92, id)], MatchedGesture: id, Stroke: stroke);
        var none = new RecognitionLogEntry(T0, 3, 120, [new("Right", 60, id)], NothingFiredReason: "below threshold", Stroke: stroke);

        Assert.Equal(id, matched.MatchedGesture);
        Assert.Equal(id, matched.TopMatches[0].Id);
        Assert.Same(stroke, matched.Stroke);
        Assert.Null(none.MatchedGesture);
        Assert.Same(stroke, none.Stroke);
    }

    [Fact]
    public void Entry_RejectsNullMatches()
    {
        Assert.Throws<ArgumentNullException>(() => new RecognitionLogEntry(T0, 1, 1, null!));
    }

    private static RecognitionLogEntry Entry(int pointCount)
        => new(T0.AddSeconds(pointCount), pointCount, 250, [new("Up", 90)], NothingFiredReason: "no command");
}
