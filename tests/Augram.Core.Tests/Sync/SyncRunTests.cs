using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Sync;

/// <summary>One run's edges: off, failures, broken files, the log line, the machine id, publishing only when needed, stores moving underneath.</summary>
public sealed class SyncRunTests : TwoMachineTest
{
    [Fact]
    public void WithoutARepositorySyncIsOffAndTouchesNothing()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        work.Settings.SetSync(work.Settings.Current.Sync with { RepositoryUrl = "  " });

        var report = work.Sync();

        Assert.Equal(SyncStatus.Off, report.Status);
        Assert.Null(work.Repository.PreparedUrl);
        Assert.Empty(Remote.Files);
    }

    [Fact]
    public void AFailingRepositoryLeavesTheStoresUntouched()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();
        home.Repository.FailWith = "offline";
        var gestures = home.Gestures.All;
        var mapping = home.Mapping.Current;

        var report = home.Sync();

        Assert.Equal(SyncStatus.Failed, report.Status);
        Assert.Equal("offline", report.Error);
        Assert.Same(gestures, home.Gestures.All);
        Assert.Same(mapping, home.Mapping.Current);
        Assert.Equal(EventLevel.Warning, home.Log.Last.Level);
        home.Repository.FailWith = null;
        Assert.Equal(SyncStatus.Applied, home.Sync().Status);
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "West");
    }

    [Fact]
    public void AFailedPublishIsRetriedByTheNextRun()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        work.Repository.FailPublishWith = "push rejected";

        var failed = work.Sync();
        work.Repository.FailPublishWith = null;
        var retried = work.Sync();

        Assert.Equal(SyncStatus.Failed, failed.Status);
        Assert.Equal("push rejected", failed.Error);
        Assert.Equal(SyncStatus.UpToDate, retried.Status);
        Assert.Single(Remote.Files);
        Assert.False(work.Bases.PublishPending);
    }

    [Fact]
    public void ABrokenFileIsReportedAndSkippedAndTheRestMerges()
    {
        var (work, home) = Joined();
        Remote.Files[Guid.NewGuid().ToString("D")] = "{ not json";
        Remote.Files["notes"] = "{}";
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Equal(2, report.Notes.Count);
        Assert.Contains(report.Notes, note => note.Contains("not valid JSON", StringComparison.Ordinal));
        Assert.Contains(report.Notes, note => note.Contains("not a machine id", StringComparison.Ordinal));
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "West");
    }

    [Fact]
    public void AFileWithStepsThisBuildCannotReadIsSkippedRatherThanMerged()
    {
        var (work, home) = Joined();
        var key = work.Id.ToString("D");
        Remote.Files[key] = Remote.Files[key].Replace("\"type\": \"fake\"", "\"type\": \"newer\"", StringComparison.Ordinal);

        var report = home.Sync();

        Assert.Contains(report.Notes, note => note.Contains("Update Augram on this machine", StringComparison.Ordinal));
        Assert.True(report.Counts.IsEmpty);
        Assert.All(home.Mapping.Current.AllCommands(), pair => Assert.NotEmpty(pair.Command.Steps));
    }

    [Fact]
    public void TheLogLineHasCountsAndTheHostButNoContentOrPath()
    {
        var (work, _) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");

        work.Sync();

        var line = work.Log.Events.Last(e => e.Source == SyncCoordinator.LogSource);
        Assert.Equal(EventLevel.Info, line.Level);
        Assert.Equal("github.com", line.Properties!.Single(property => property.Key == "host").Value);
        Assert.Contains(line.Properties!, property => property.Key == "changes");
        var text = line.Message + string.Join(" ", line.Properties!.Select(property => property.Value));
        Assert.DoesNotContain("augram-settings", text, StringComparison.Ordinal);
        Assert.DoesNotContain("West", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMachineIdIsGeneratedOnceAndCannotBeUndone()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup(), Guid.Empty);
        work.Settings.SetSync(work.Settings.Current.Sync with { AutoSync = false });

        work.Sync();
        var id = work.Id;
        work.Sync();

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(id, work.Id);
        Assert.True(Remote.Files.ContainsKey(id.ToString("D")));
        Assert.False(work.Settings.CanUndo);
    }

    [Fact]
    public void NothingNewMeansNothingPublished()
    {
        var (work, home) = Joined();
        work.Sync();
        home.Sync();
        int published = Remote.Messages.Count;

        work.Sync();
        home.Sync();
        work.Sync();

        Assert.Equal(published, Remote.Messages.Count);
    }

    [Fact]
    public void ANewMachineNameIsPublishedAndShownElsewhere()
    {
        var (work, home) = Joined();
        work.Sync();
        home.Sync();
        int published = Remote.Messages.Count;

        work.Settings.SetSync(work.Settings.Current.Sync with { MachineName = "OFFICE" });
        work.Sync();
        home.Mapping.UpdateCommand(Core.Mapping.GroupId.Global, home.Command("Close") with { Name = "Close app" });
        work.Mapping.UpdateCommand(Core.Mapping.GroupId.Global, work.Command("Close") with { Name = "Close window" });
        work.Sync();

        Assert.Equal(published + 2, Remote.Messages.Count);
        Assert.Equal("sync from OFFICE", Remote.Messages[^1]);
        Assert.Equal("OFFICE", Assert.Single(home.Sync().Conflicts).MachineName);
    }

    [Fact]
    public void StoresThatMoveDuringTheSyncAreMergedAgain()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();
        bool edited = false;
        home.StoreThread = apply =>
        {
            if (!edited)
            {
                edited = true;
                home.Gestures.Rename(home.Gesture("Down").Id, "South");
            }

            return apply();
        };

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "West");
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "South");
    }

    [Fact]
    public void TheMachineFileIsTheConfigFilesGesturesAndMappingUnderAHeader()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());

        work.Sync();

        var file = SyncFileSerializer.Read(Remote.Files[work.Id.ToString("D")], Mapping.Support.FakeStepType.Registry, notice: null);
        Assert.Equal(work.Id, file.MachineId);
        Assert.Equal("PC-WORK", file.MachineName);
        Assert.True(SyncItemSet.From(file.Gestures, file.Mapping).SameAs(work.Items.Contents()));
    }
}
