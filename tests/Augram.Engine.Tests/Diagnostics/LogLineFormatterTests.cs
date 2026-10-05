using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Xunit;

namespace Augram.Engine.Tests.Diagnostics;

/// <summary>The one line shape (N4), byte for byte.</summary>
public sealed class LogLineFormatterTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 5, 21, 14, 3, 123, TimeSpan.FromHours(2));

    [Fact]
    public void InfoWithOneProperty_MatchesTheSpecimen()
    {
        var e = new LogEvent(Stamp, EventLevel.Info, "hook", "Hook installed", [new("generation", 2)]);

        Assert.Equal("2026-10-05T21:14:03.123+02:00 INFO  hook      Hook installed {generation=2}", LogLineFormatter.Format(e));
    }

    [Fact]
    public void NoProperties_NoBraces()
    {
        var e = new LogEvent(Stamp, EventLevel.Warning, "config", "Migrated");

        Assert.Equal("2026-10-05T21:14:03.123+02:00 WARN  config    Migrated", LogLineFormatter.Format(e));
    }

    [Fact]
    public void LongSource_IsNotTruncated()
    {
        var e = new LogEvent(Stamp, EventLevel.Trace, "recognition", "Ranked");

        Assert.Equal("2026-10-05T21:14:03.123+02:00 TRACE recognition Ranked", LogLineFormatter.Format(e));
    }

    [Fact]
    public void Values_AreInvariantAndSpaceSeparated()
    {
        var e = new LogEvent(Stamp, EventLevel.Debug, "capture", "Stroke",
        [
            new("points", 37),
            new("latencyMs", 3.25),
            new("ok", true),
            new("group", null),
            new("name", "New tab"),
            new("level", EventLevel.Error),
        ]);

        Assert.EndsWith(" DEBUG capture   Stroke {points=37 latencyMs=3.25 ok=true group=null name=New tab level=Error}", LogLineFormatter.Format(e), StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeOffset_AndMidnight_RenderCorrectly()
    {
        var e = new LogEvent(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.FromHours(-5)), EventLevel.Error, "s", "m");

        Assert.StartsWith("2026-01-02T00:00:00.000-05:00 ERROR s         m", LogLineFormatter.Format(e), StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_FollowsOnIndentedLines()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        var e = new LogEvent(Stamp, EventLevel.Error, "steps", "Step failed", [new("step", "Hotkey")], caught);

        var lines = LogLineFormatter.Format(e).Split(Environment.NewLine);
        Assert.Equal("2026-10-05T21:14:03.123+02:00 ERROR steps     Step failed {step=Hotkey}", lines[0]);
        Assert.True(lines.Length >= 3, "expected the exception type line and at least one stack frame");
        Assert.All(lines.Skip(1), line => Assert.StartsWith(LogLineFormatter.ExceptionIndent, line, StringComparison.Ordinal));
        Assert.Contains("InvalidOperationException: boom", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_AreFiveCharactersOrFewer()
    {
        foreach (var level in Enum.GetValues<EventLevel>())
        {
            Assert.InRange(LogLineFormatter.Label(level).Length, 4, 5);
        }
    }
}
