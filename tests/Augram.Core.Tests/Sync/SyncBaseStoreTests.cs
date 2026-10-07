using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Config;
using Xunit;

namespace Augram.Core.Tests.Sync;

public sealed class SyncBaseStoreTests : IDisposable
{
    private static readonly SyncItemKey Command = SyncItemKey.ForCommand(CommandId.New());
    private static readonly SyncItemKey Category = SyncItemKey.ForCategory(GroupId.Global, CategoryId.New());

    private readonly TempFolder _folder = new();
    private readonly List<string> _notices = [];

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AMachineStateRoundTripsWithItsBaseHeldKeysAndConflicts()
    {
        var store = Store();
        var id = Guid.NewGuid();
        var conflict = new SyncConflict(Command, "Close \"tab\"", "{\"name\":\"a\"}", null)
        {
            MachineId = id,
            MachineName = "PC-WORK",
            DetectedAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero),
        };
        var state = new SyncMachineState(id, "PC-WORK")
        {
            Base = new Dictionary<SyncItemKey, string> { [Command] = "{\"name\":\"a\"}", [Category] = "{\"name\":\"Media\"}" },
            MergedRevision = Guid.NewGuid(),
            AppliedAcknowledgement = Guid.NewGuid(),
            Held = [Category],
            Conflicts = [conflict],
            LastMerged = new DateTimeOffset(2026, 10, 7, 9, 1, 0, TimeSpan.Zero),
        };

        store.Save(state);
        var back = Store().Machine(id)!;

        Assert.Equal(state.Base, back.Base);
        Assert.Equal((state.MergedRevision, state.AppliedAcknowledgement, state.LastMerged), (back.MergedRevision, back.AppliedAcknowledgement, back.LastMerged));
        Assert.Equal([Category], back.Held);
        Assert.Equal(conflict, Assert.Single(back.Conflicts));
        Assert.Equal(conflict, Assert.Single(Store().PendingConflicts()));
        Assert.False(Store().IsEmpty);
    }

    [Fact]
    public void TheAcknowledgementExceptsConflictsAndHeldKeysAndMarksConflictsPending()
    {
        var state = new SyncMachineState(Guid.NewGuid(), "PC")
        {
            MergedRevision = Guid.NewGuid(),
            Held = [Category],
            Conflicts = [new SyncConflict(Command, "Close", "a", "b")],
        };

        var entry = state.Acknowledgement()!;

        Assert.Equal(state.MergedRevision, entry.Revision);
        Assert.Equal([Command.ToString(), Category.ToString()], entry.Except);
        Assert.Equal([Command.ToString()], entry.Pending);
        Assert.Null(new SyncMachineState(Guid.NewGuid(), "PC").Acknowledgement());
    }

    [Fact]
    public void PublishedRevisionsAreFoundByIdAndTheNewestTwentyAreKept()
    {
        var store = Store();
        var revisions = Enumerable.Range(0, SyncBaseStore.KeepPublished + 3).Select(_ => Guid.NewGuid()).ToArray();
        var earlier = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

        foreach (var revision in revisions)
        {
            store.Save(new SyncPublished(revision, earlier, new Dictionary<SyncItemKey, string> { [Command] = revision.ToString() }, []));
        }

        Assert.Equal(revisions[^1], store.LastPublished()!.Revision);
        Assert.Equal(revisions[^2].ToString(), store.Published(revisions[^2])!.Items[Command]);
        Assert.Null(store.Published(revisions[0]));
        Assert.NotNull(store.Published(revisions[3]));
    }

    [Fact]
    public void AnUnreadableStateFileIsReportedAndTreatedAsAbsent()
    {
        var store = Store();
        var id = Guid.NewGuid();
        store.Save(new SyncMachineState(id, "PC"));
        File.WriteAllText(Path.Combine(store.Folder, "machines", $"{id:D}.json"), "{ broken");

        Assert.Null(store.Machine(id));
        Assert.Empty(store.Machines());
        Assert.Contains(_notices, notice => notice.Contains("ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void ANewStoreIsEmptyAndLivesUnderSyncState()
    {
        var store = Store();

        Assert.True(store.IsEmpty);
        Assert.Null(store.LastPublished());
        Assert.False(store.PublishPending);
        Assert.Equal(Path.Combine(_folder.Path, "sync", "state"), store.Folder);
        store.PublishPending = true;
        Assert.True(Store().PublishPending);
        Assert.True(Store().IsEmpty);
    }

    private SyncBaseStore Store() => new(_folder.Path, _notices.Add);
}
