using Augram.App.Tests.Sync.Support;
using Augram.Core.Gestures;
using Augram.Core.Sync;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>Base for the App's sync tests: machines on one shared in-memory repo, disposed (services stopped, temp folders removed) with the test.</summary>
public abstract class SyncTestBase : IDisposable
{
    private readonly List<SyncMachine> _machines = [];

    internal SyncRemote Remote { get; } = new();

    public void Dispose()
    {
        foreach (var machine in _machines)
        {
            machine.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    internal SyncMachine Machine(string name, IEnumerable<Gesture>? gestures = null, string? url = SyncMachine.Url, bool autoSync = true)
    {
        var machine = new SyncMachine(name, Remote, gestures, url: url, autoSync: autoSync);
        _machines.Add(machine);
        return machine;
    }

    /// <summary>PC-WORK with gestures Up and Down, its file published (its service never started).</summary>
    internal SyncMachine Work()
    {
        var work = Machine("PC-WORK", [SyncMachine.NewGesture("Up", 0, -100), SyncMachine.NewGesture("Down", 0, 100)]);
        Assert.Equal(SyncStatus.UpToDate, work.SyncDirect().Status);
        return work;
    }

    /// <summary>PC-WORK published, then PC-HOME (one gesture of its own) joined it with "use the synced settings", straight through its coordinator; PC-HOME's service is not started yet.</summary>
    internal (SyncMachine Work, SyncMachine Home) Joined()
    {
        var work = Work();
        var home = Machine("PC-HOME", [SyncMachine.NewGesture("Mine")]);
        Assert.Equal(SyncStatus.Applied, home.SyncDirect(SyncJoin.UseRemote).Status);
        return (work, home);
    }
}
