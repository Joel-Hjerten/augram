using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Xunit;

namespace Augram.Engine.Tests.Diagnostics;

/// <summary>The live tail ring: last 2,000 by default, newest kept, version bumps per write.</summary>
public sealed class InMemorySinkTests
{
    [Fact]
    public void DefaultCapacity_Is2000()
    {
        using var sink = new InMemorySink();

        Assert.Equal(2000, sink.Capacity);
    }

    [Fact]
    public void KeepsTheNewestInOrder()
    {
        using var sink = new InMemorySink(capacity: 3);

        for (var i = 0; i < 10; i++)
        {
            sink.Write(Event(i));
        }

        sink.Flush();

        Assert.Equal(["7", "8", "9"], sink.Snapshot().Select(e => e.Message));
        Assert.Equal(10, sink.Version);
        Assert.Equal(3, sink.Count);
    }

    [Fact]
    public void WorksAsAChannelSink()
    {
        var sink = new InMemorySink(capacity: 5);
        using (var log = new ChannelEventLog([sink]))
        {
            for (var i = 0; i < 8; i++)
            {
                log.Info("test", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        Assert.Equal(["3", "4", "5", "6", "7"], sink.Snapshot().Select(e => e.Message));
    }

    private static LogEvent Event(int n) => new(DateTimeOffset.Now, EventLevel.Info, "test", n.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
