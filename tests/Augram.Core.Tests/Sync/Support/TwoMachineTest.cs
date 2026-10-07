using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Xunit;

namespace Augram.Core.Tests.Sync.Support;

/// <summary>Base for the end-to-end sync tests: machines on one shared in-memory repo and one clock, disposed with the test.</summary>
public abstract class TwoMachineTest : IDisposable
{
    private readonly List<SyncMachine> _machines = [];

    internal SyncRemote Remote { get; } = new();

    internal SteppingClock Clock { get; } = new();

    public void Dispose()
    {
        foreach (var machine in _machines)
        {
            machine.Dispose();
        }
    }

    internal SyncMachine Machine(string name, (Gesture[] Gestures, MappingDocument Mapping)? setup = null, Guid? id = null)
    {
        var machine = new SyncMachine(name, Remote, Clock, setup?.Gestures, setup?.Mapping, id);
        _machines.Add(machine);
        return machine;
    }

    /// <summary>PC-WORK has the sample setup and syncs first; PC-HOME, with one gesture of its own, joins it with "use the synced settings".</summary>
    internal (SyncMachine Work, SyncMachine Home) Joined()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var home = Machine("PC-HOME", ([SyncSamples.NewGesture("Mine")], MappingDocument.Empty));
        Assert.Equal(SyncStatus.UpToDate, work.Sync().Status);
        Assert.Equal(SyncStatus.Applied, home.Sync(SyncJoin.UseRemote).Status);
        return (work, home);
    }

    /// <summary>
    /// After <see cref="Joined"/>, both rename the global command "Close" differently; work syncs first, then home
    /// finds the conflict, then work syncs again and holds the item while home decides.
    /// </summary>
    internal (SyncMachine Work, SyncMachine Home, SyncConflict Conflict) Conflicted()
    {
        var (work, home) = Joined();
        work.Mapping.UpdateCommand(GroupId.Global, work.Command("Close") with { Name = "Close window" });
        home.Mapping.UpdateCommand(GroupId.Global, home.Command("Close") with { Name = "Close app" });
        Assert.Empty(work.Sync().Conflicts);
        var conflict = Assert.Single(home.Sync().Conflicts);
        Assert.Empty(work.Sync().Conflicts);
        return (work, home, conflict);
    }
}
