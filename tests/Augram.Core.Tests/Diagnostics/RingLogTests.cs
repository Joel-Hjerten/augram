using Augram.Core.Diagnostics;
using Xunit;

namespace Augram.Core.Tests.Diagnostics;

/// <summary>Fixed capacity, oldest-first snapshots, version counter and change event of the ring behind every live view.</summary>
public sealed class RingLogTests
{
    [Fact]
    public void BelowCapacity_SnapshotIsEverythingInOrder()
    {
        var ring = new RingLog<int>(5);

        ring.Add(1);
        ring.Add(2);
        ring.Add(3);

        Assert.Equal([1, 2, 3], ring.Snapshot());
        Assert.Equal(3, ring.Count);
        Assert.Equal(5, ring.Capacity);
    }

    [Fact]
    public void PastCapacity_KeepsTheNewestOldestFirst()
    {
        var ring = new RingLog<int>(3);

        for (var i = 1; i <= 10; i++)
        {
            ring.Add(i);
        }

        Assert.Equal([8, 9, 10], ring.Snapshot());
        Assert.Equal(3, ring.Count);
    }

    [Fact]
    public void ExactlyAtCapacity_WrapsCleanly()
    {
        var ring = new RingLog<int>(3);

        ring.Add(1);
        ring.Add(2);
        ring.Add(3);
        Assert.Equal([1, 2, 3], ring.Snapshot());

        ring.Add(4);
        Assert.Equal([2, 3, 4], ring.Snapshot());
    }

    [Fact]
    public void Version_IncrementsPerAddAndClear()
    {
        var ring = new RingLog<string>(2);
        Assert.Equal(0, ring.Version);

        ring.Add("a");
        ring.Add("b");
        ring.Add("c");
        Assert.Equal(3, ring.Version);

        ring.Clear();
        Assert.Equal(4, ring.Version);
        Assert.Empty(ring.Snapshot());
        Assert.Equal(0, ring.Count);
    }

    [Fact]
    public void Changed_FiresAfterEachAddWithTheItemAlreadyVisible()
    {
        var ring = new RingLog<int>(4);
        var seen = new List<int>();
        ring.Changed += (sender, _) => seen.Add(((RingLog<int>)sender!).Snapshot()[^1]);

        ring.Add(7);
        ring.Add(8);

        Assert.Equal([7, 8], seen);
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        var ring = new RingLog<int>(4);
        ring.Add(1);

        var first = ring.Snapshot();
        ring.Add(2);

        Assert.Equal([1], first);
        Assert.Equal([1, 2], ring.Snapshot());
    }

    [Fact]
    public void ZeroCapacity_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingLog<int>(0));
    }

    [Fact]
    public void ConcurrentReaders_SeeConsistentSnapshots()
    {
        var ring = new RingLog<int>(64);
        using var stop = new CancellationTokenSource();
        var writer = new Thread(() =>
        {
            var next = 0;
            while (!stop.IsCancellationRequested)
            {
                ring.Add(next++);
            }
        });
        writer.Start();

        for (var i = 0; i < 2000; i++)
        {
            var snapshot = ring.Snapshot();
            for (var j = 1; j < snapshot.Count; j++)
            {
                Assert.Equal(snapshot[j - 1] + 1, snapshot[j]);
            }
        }

        stop.Cancel();
        writer.Join();
    }
}
