using System.Text;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Xunit;

namespace Augram.Engine.Tests.Diagnostics;

/// <summary>Daily files, the line format, retention by file date, and flushing (N4).</summary>
public sealed class RollingFileSinkTests : IDisposable
{
    private static readonly DateTimeOffset Oct5 = new(2026, 10, 5, 21, 14, 3, 123, TimeSpan.FromHours(2));
    private readonly TempDirectory _dir = new();
    private DateTimeOffset _now = Oct5;

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void WritesTheExpectedLineToTodaysFile()
    {
        using (var sink = new RollingFileSink(_dir.Path, () => _now))
        {
            sink.Write(new LogEvent(Oct5, EventLevel.Info, "hook", "Hook installed", [new("generation", 2)]));
            Assert.Equal(_dir.File("augram-20261005.log"), sink.CurrentFilePath);
        }

        Assert.Equal(["augram-20261005.log"], _dir.FileNames());
        Assert.Equal(
            "2026-10-05T21:14:03.123+02:00 INFO  hook      Hook installed {generation=2}" + Environment.NewLine,
            File.ReadAllText(_dir.File("augram-20261005.log")));
    }

    [Fact]
    public void FileIsUtf8WithoutBom_AndAppendsAcrossInstances()
    {
        using (var sink = new RollingFileSink(_dir.Path, () => _now))
        {
            sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "första"));
        }

        using (var sink = new RollingFileSink(_dir.Path, () => _now))
        {
            sink.Write(new LogEvent(Oct5.AddMinutes(1), EventLevel.Info, "s", "andra"));
        }

        var bytes = File.ReadAllBytes(_dir.File("augram-20261005.log"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "BOM present");
        var lines = Encoding.UTF8.GetString(bytes).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("första", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("andra", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RotatesWhenTheEventDateChanges()
    {
        using (var sink = new RollingFileSink(_dir.Path, () => _now))
        {
            sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "day one"));
            sink.Write(new LogEvent(Oct5.AddHours(2), EventLevel.Info, "s", "day one, late"));
            _now = Oct5.AddDays(1);
            sink.Write(new LogEvent(_now, EventLevel.Info, "s", "day two"));
            Assert.Equal(_dir.File("augram-20261006.log"), sink.CurrentFilePath);
        }

        Assert.Equal(["augram-20261005.log", "augram-20261006.log"], _dir.FileNames());
        Assert.Equal(2, File.ReadAllLines(_dir.File("augram-20261005.log")).Length);
        Assert.EndsWith("day two", File.ReadAllText(_dir.File("augram-20261006.log")).TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void DeletesFilesOlderThanRetentionAtStartup_KeepingTheRest()
    {
        Touch("augram-20260920.log");   // 15 days old: gone
        Touch("augram-20260927.log");   // 8 days old: gone
        Touch("augram-20260928.log");   // 7 days old: kept
        Touch("augram-20261005.log");   // today: kept
        Touch("augram-20261101.log");   // future (clock skew): kept
        Touch("augram-latest.log");     // not ours: kept
        Touch("notes.txt");             // not ours: kept

        using (new RollingFileSink(_dir.Path, () => _now))
        {
        }

        Assert.Equal(["augram-20260928.log", "augram-20261005.log", "augram-20261101.log", "augram-latest.log", "notes.txt"], _dir.FileNames());
    }

    [Fact]
    public void DeletesExpiredFilesAtRotationToo()
    {
        using var sink = new RollingFileSink(_dir.Path, () => _now, retainDays: 2);
        sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "day 5"));
        Touch("augram-20261003.log");

        _now = Oct5.AddDays(3);
        sink.Write(new LogEvent(_now, EventLevel.Info, "s", "day 8"));

        Assert.Equal(["augram-20261008.log"], _dir.FileNames());
    }

    [Fact]
    public void RetentionZero_KeepsOnlyToday()
    {
        Touch("augram-20261004.log");
        Touch("augram-20261005.log");

        using (new RollingFileSink(_dir.Path, () => _now, retainDays: 0))
        {
        }

        Assert.Equal(["augram-20261005.log"], _dir.FileNames());
    }

    [Fact]
    public void FlushOnDispose_AndExplicitFlush_MakeLinesVisible()
    {
        var sink = new RollingFileSink(_dir.Path, () => _now);
        sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "one"));
        sink.Flush();
        Assert.EndsWith("one", TempDirectory.ReadShared(sink.CurrentFilePath!).TrimEnd(), StringComparison.Ordinal);

        sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "two"));
        sink.Dispose();
        sink.Dispose();

        Assert.Equal(2, File.ReadAllLines(_dir.File("augram-20261005.log")).Length);
    }

    [Fact]
    public void TimerFlushesWithinTheInterval_WithoutAnyFlushCall()
    {
        using var sink = new RollingFileSink(_dir.Path, () => _now);
        sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "timer"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (new FileInfo(sink.CurrentFilePath!).Length == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }

        Assert.EndsWith("timer", TempDirectory.ReadShared(sink.CurrentFilePath!).TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAfterDispose_IsIgnored()
    {
        var sink = new RollingFileSink(_dir.Path, () => _now);
        sink.Dispose();

        sink.Write(new LogEvent(Oct5, EventLevel.Info, "s", "late"));

        Assert.Empty(_dir.FileNames());
    }

    [Fact]
    public void CreatesTheDirectory()
    {
        var nested = Path.Combine(_dir.Path, "logs", "deeper");

        using var sink = new RollingFileSink(nested, () => _now);

        Assert.True(Directory.Exists(nested));
        Assert.Equal(Path.GetFullPath(nested), sink.DirectoryPath);
    }

    [Theory]
    [InlineData("augram-20261005.log", true, 2026, 10, 5)]
    [InlineData("augram-20261005.log.bak", false, 0, 0, 0)]
    [InlineData("augram-2026100.log", false, 0, 0, 0)]
    [InlineData("augram-20261345.log", false, 0, 0, 0)]
    [InlineData("other-20261005.log", false, 0, 0, 0)]
    public void FileDateParsing(string name, bool expected, int y, int m, int d)
    {
        Assert.Equal(expected, RollingFileSink.TryParseFileDate(name, out var date));
        if (expected)
        {
            Assert.Equal(new DateOnly(y, m, d), date);
            Assert.Equal(name, RollingFileSink.FileNameFor(date));
        }
    }

    private void Touch(string name) => File.WriteAllText(_dir.File(name), string.Empty);
}
