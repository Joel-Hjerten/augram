using Augram.App.Tests.Architecture;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.App.Tests.Import;

public sealed class ImportViewModelTests
{
    private static readonly string Fixture = Path.Combine(RepositoryPaths.Root, "tests", "Augram.Core.Tests", "Fixtures", "StrokesPlusNet", "sample-config.json");

    [Fact]
    public void LoadShowsStatsWarningsAndThePlanAgainstTheLibrary()
    {
        var mine = new GestureLibrary([Existing("Synthetic L Shape"), Existing("synthetic circle"), Existing("Keep")]);
        var vm = new ImportViewModel(mine, new RecordingEventLog());

        vm.Load(Fixture);

        Assert.Null(vm.Error);
        Assert.StartsWith("Found ", vm.StatsText, StringComparison.Ordinal);
        Assert.Contains("will import in a later version", vm.StatsText, StringComparison.Ordinal);
        Assert.True(vm.HasWarnings);
        Assert.Contains(vm.Warnings, line => line.Contains("Synthetic Empty", StringComparison.Ordinal));
        Assert.Equal(["Synthetic L Shape", "Synthetic Circle"], vm.Conflicts.Select(c => c.Name));
        Assert.All(vm.Conflicts, c => Assert.Equal(MergeChoice.KeepMine, c.Choice));
        Assert.True(vm.CanApply);
        Assert.Equal(Fixture, vm.SourcePath);
    }

    [Fact]
    public void ApplyMergesWithMixedChoicesAsOneUndoStepAndLogsTheCounts()
    {
        var lShape = Existing("Synthetic L Shape");
        var circle = Existing("Synthetic Circle");
        var mine = new GestureLibrary([lShape, circle, Existing("Keep")]);
        var log = new RecordingEventLog();
        var vm = new ImportViewModel(mine, log);
        var finished = 0;
        vm.Finished += (_, _) => finished++;
        vm.Load(Fixture);
        vm.ApplyToAll(MergeChoice.KeepBoth);
        vm.Conflicts.Single(c => c.Name == "Synthetic L Shape").Choice = MergeChoice.TakeTheirs;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(1, finished);
        Assert.Equal(2, mine.Find(lShape.Id)!.Samples.Count);
        Assert.Equal(circle, mine.Find(circle.Id));
        Assert.Contains(mine.All, g => g.Name == "Synthetic Circle (imported)");
        Assert.Contains(mine.All, g => g.Name == "Synthetic Up Stock");
        Assert.Contains(mine.All, g => g.Name == "Keep");
        Assert.True(mine.CanUndo);

        var entry = Assert.Single(log.Events, e => e.Source == ImportViewModel.LogSource);
        Assert.Equal("Gestures imported from StrokesPlus.net", entry.Message);
        Assert.Contains(entry.Properties!, p => p.Key == "replaced" && Equals(p.Value, 1));
        Assert.Contains(entry.Properties!, p => p.Key == "kept" && Equals(p.Value, 0));

        mine.Undo();
        Assert.Equal(3, mine.All.Count);
    }

    [Fact]
    public void UnreadableFileBecomesAnErrorNotAnException()
    {
        var vm = new ImportViewModel(new GestureLibrary(), new RecordingEventLog());

        vm.Load(Path.Combine(Path.GetTempPath(), "augram-missing-" + Guid.NewGuid().ToString("N") + ".json"));

        Assert.NotNull(vm.Error);
        Assert.False(vm.CanApply);
    }

    private static Gesture Existing(string name)
        => new(GestureId.New(), name, IsActive: true, [new GestureSample([new(0, 0), new(0, 100)])]);
}
