using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// The sync format guard (README: format version): a build never merges or overwrites a file a newer Augram wrote,
/// and never publishes this machine's file below the format it already published. Older files still merge.
/// </summary>
public sealed class SyncFormatVersionTests : TwoMachineTest
{
    private static readonly string ThisFormat = $"\"formatVersion\": {SyncFile.CurrentFormatVersion}";
    private static readonly string NextFormat = $"\"formatVersion\": {SyncFile.CurrentFormatVersion + 1}";

    [Fact]
    public void AFileOneFormatNewerPausesSyncAndLeavesStoresStateAndRepoUntouched()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();
        Rewrite(work, ThisFormat, NextFormat);
        var gestures = home.Gestures.All;
        var mapping = home.Mapping.Current;
        var state = StateFiles(home);
        var repo = new Dictionary<string, string>(Remote.Files);
        int commits = Remote.Messages.Count;

        var report = home.Sync();

        Assert.Equal(SyncStatus.NeedsUpdate, report.Status);
        var newer = Assert.Single(report.NewerMachines);
        Assert.Equal(("PC-WORK", false, SyncFile.CurrentFormatVersion + 1), (newer.MachineName, newer.IsThisMachine, newer.FormatVersion));
        Assert.Equal("PC-WORK uses a newer Augram", SyncNewerMachine.Summary(report.NewerMachines));
        Assert.Contains(newer.Description, report.Notes);
        Assert.True(report.Counts.IsEmpty);
        Assert.Same(gestures, home.Gestures.All);
        Assert.Same(mapping, home.Mapping.Current);
        Assert.Equal(state, StateFiles(home));
        Assert.Equal(repo, Remote.Files);
        Assert.Equal(commits, Remote.Messages.Count);
        Assert.Equal(EventLevel.Warning, home.Log.Last.Level);
    }

    [Fact]
    public void AFileInThisBuildsFormatSyncsNormally()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();
        Assert.Contains(ThisFormat, Remote.Files[work.Id.ToString("D")], StringComparison.Ordinal);

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Empty(report.NewerMachines);
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "West");
    }

    [Fact]
    public void AnOlderFileStillMerges()
    {
        var (work, home) = Joined();
        work.Gestures.Rename(work.Gesture("Left").Id, "West");
        work.Sync();
        Rewrite(work, ThisFormat + ",", string.Empty);

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Empty(report.Notes);
        Assert.Contains(home.Gestures.All, gesture => gesture.Name == "West");
    }

    [Fact]
    public void ANewerConfigSchemaPausesSyncToo()
    {
        var (work, home) = Joined();
        Rewrite(work, $"\"schemaVersion\": {ConfigDocument.CurrentSchemaVersion}", $"\"schemaVersion\": {ConfigDocument.CurrentSchemaVersion + 1}");

        var report = home.Sync();

        Assert.Equal(SyncStatus.NeedsUpdate, report.Status);
        Assert.Contains("config schema", Assert.Single(report.NewerMachines).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void AMachineJoiningARepoWithANewerFileIsToldToUpdateBeforeTheJoinQuestion()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        work.Sync();
        Rewrite(work, ThisFormat, NextFormat);
        var fresh = Machine("PC-NEW");

        var report = fresh.Sync();

        Assert.Equal(SyncStatus.NeedsUpdate, report.Status);
        Assert.True(fresh.Bases.IsEmpty);
    }

    [Fact]
    public void ThisMachinesOwnFileInANewerFormatIsNotRewritten()
    {
        var (_, home) = Joined();
        Rewrite(home, ThisFormat, NextFormat);
        var own = Remote.Files[home.Id.ToString("D")];
        home.Gestures.Rename(home.Gesture("Left").Id, "West");

        var report = home.Sync();

        Assert.Equal(SyncStatus.NeedsUpdate, report.Status);
        Assert.True(Assert.Single(report.NewerMachines).IsThisMachine);
        Assert.Equal(own, Remote.Files[home.Id.ToString("D")]);
    }

    [Fact]
    public void AMachineThatPublishedANewerFormatNeverPublishesAnOlderOne()
    {
        var (_, home) = Joined();
        home.Bases.RaisePublishedFormatVersion(SyncFile.CurrentFormatVersion + 1);
        home.Gestures.Rename(home.Gesture("Left").Id, "West");
        int commits = Remote.Messages.Count;

        var report = home.Sync();

        Assert.Equal(SyncStatus.NeedsUpdate, report.Status);
        var self = Assert.Single(report.NewerMachines);
        Assert.Equal((home.Id, true, SyncFile.CurrentFormatVersion + 1), (self.MachineId, self.IsThisMachine, self.FormatVersion));
        Assert.Equal("this machine last synced with a newer Augram", SyncNewerMachine.Summary(report.NewerMachines));
        Assert.Equal(commits, Remote.Messages.Count);
    }

    [Fact]
    public void ThePublishedFormatIsRecordedBeforeThePushAndNeverLowered()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        Assert.Equal(0, work.Bases.PublishedFormatVersion);
        work.Repository.FailPublishWith = "push rejected";

        work.Sync();
        work.Bases.RaisePublishedFormatVersion(1);

        Assert.Equal(SyncFile.CurrentFormatVersion, work.Bases.PublishedFormatVersion);
        work.Bases.Clear();
        Assert.Equal(0, work.Bases.PublishedFormatVersion);
    }

    [Fact]
    public void TheSummaryNamesEveryOtherMachine()
    {
        SyncNewerMachine Newer(string name, bool self = false) => new(Guid.NewGuid(), name, self, SyncFile.CurrentFormatVersion + 1, ConfigDocument.CurrentSchemaVersion);

        Assert.Equal("Mac and PC-WORK use a newer Augram", SyncNewerMachine.Summary([Newer("Mac"), Newer("PC-WORK"), Newer("PC-HOME", self: true)]));
        Assert.Equal("Mac, Laptop and PC-WORK use a newer Augram", SyncNewerMachine.Summary([Newer("Mac"), Newer("Laptop"), Newer("PC-WORK")]));
    }

    private void Rewrite(SyncMachine machine, string from, string to)
    {
        var key = machine.Id.ToString("D");
        Assert.Contains(from, Remote.Files[key], StringComparison.Ordinal);
        Remote.Files[key] = Remote.Files[key].Replace(from, to, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> StateFiles(SyncMachine machine)
        => Directory.EnumerateFiles(machine.Bases.Folder, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText);
}
