using Augram.App.Tests.Architecture;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.App.Tests.Import;

public sealed class ImportViewModelTests
{
    private static readonly string Fixture = FixturePath("sample-config.json");
    private static readonly string FullFixture = FixturePath("sample-config-full.json");

    private static string FixturePath(string name)
        => Path.Combine(RepositoryPaths.Root, "tests", "Augram.Core.Tests", "Fixtures", "StrokesPlusNet", name);

    [Fact]
    public void LoadShowsStatsWarningsAndThePlanAgainstTheLibrary()
    {
        var mine = new GestureLibrary([Existing("Synthetic L Shape"), Existing("synthetic circle"), Existing("Keep")]);
        var vm = new ImportViewModel(mine, new MappingStore(), new RecordingEventLog());

        vm.Load(Fixture);

        Assert.Null(vm.Error);
        Assert.Equal("Found 12 gestures, 1 app groups, 3 commands (1 steps as placeholders until their step types exist), 0 excluded apps.", vm.StatsText);
        Assert.True(vm.HasWarnings);
        Assert.Contains(vm.Warnings, line => line.Contains("Synthetic Empty", StringComparison.Ordinal));
        Assert.Equal(["Synthetic L Shape", "Synthetic Sleeper", "Synthetic Circle"], vm.Conflicts.Select(c => c.Name));
        Assert.Equal(MergeKind.SameShape, vm.Conflicts.Single(c => c.Name == "Synthetic Sleeper").Kind);
        Assert.Contains("same shape as 'Synthetic L Shape'", vm.Conflicts.Single(c => c.Name == "Synthetic Sleeper").Summary, StringComparison.Ordinal);
        Assert.All(vm.Conflicts, c => Assert.Equal(MergeChoice.KeepMine, c.Choice));
        Assert.Equal("7 new gesture(s) will be added; 3 already exist by name or shape. 1 group(s) and 3 command(s) will be added; 0 command(s) already exist and are skipped; 0 excluded app(s) will be added.", vm.PlanText);
        Assert.True(vm.CanApply);
        Assert.Equal(Fixture, vm.SourcePath);
    }

    [Fact]
    public void ApplyMergesWithMixedChoicesIntoBothStoresAndLogsTheCounts()
    {
        var lShape = Existing("Synthetic L Shape");
        var circle = Existing("Synthetic Circle");
        var mine = new GestureLibrary([lShape, circle, Existing("Keep")]);
        var mapping = new MappingStore();
        var log = new RecordingEventLog();
        var vm = new ImportViewModel(mine, mapping, log);
        var finished = 0;
        vm.Finished += (_, _) => finished++;
        vm.Load(Fixture);
        vm.ApplyToAll(MergeChoice.KeepBoth);
        vm.Conflicts.Single(c => c.Name == "Synthetic L Shape").Choice = MergeChoice.TakeTheirs;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(1, finished);
        Assert.Equal(2, mine.Find(lShape.Id)!.Samples.Count);
        Assert.Equal(circle, mine.Find(circle.Id));
        Assert.Contains(mine.All, g => g.Name == "Synthetic Circle (2)");
        Assert.Contains(mine.All, g => g.Name == "Synthetic Sleeper");
        Assert.Contains(mine.All, g => g.Name == "Keep");
        Assert.True(mine.CanUndo);

        Assert.Equal(["Global", "Synthetic Browser"], mapping.Current.Groups.Select(g => g.Name));
        var upStock = mine.All.Single(g => g.Name == "Synthetic Up Stock");
        var close = mapping.Global.Commands.Single(c => c.Name == "Synthetic Close Window");
        Assert.Equal(Trigger.ForGesture(upStock.Id), close.Trigger);
        Assert.Equal(Trigger.None, mapping.Global.Commands.Single(c => c.Name == "Synthetic Dangling Reference").Trigger);
        Assert.True(mapping.CanUndo);

        var entry = Assert.Single(log.Events, e => e.Source == ImportViewModel.LogSource);
        Assert.Equal(ImportViewModel.LogMessage, entry.Message);
        Assert.Contains(entry.Properties!, p => p.Key == "replaced" && Equals(p.Value, 1));
        Assert.Contains(entry.Properties!, p => p.Key == "kept" && Equals(p.Value, 0));
        Assert.Contains(entry.Properties!, p => p.Key == "groupsAdded" && Equals(p.Value, 1));
        Assert.Contains(entry.Properties!, p => p.Key == "commandsAdded" && Equals(p.Value, 3));
        Assert.Contains(entry.Properties!, p => p.Key == "commandsSkipped" && Equals(p.Value, 0));

        mine.Undo();
        mapping.Undo();
        Assert.Equal(3, mine.All.Count);
        Assert.Single(mapping.Current.Groups);
    }

    [Fact]
    public void PlanTextRecomputesWhenAChoiceChanges()
    {
        var upStock = new Gesture(GestureId.New(), "Synthetic Up Stock", IsActive: true, [new GestureSample([new(0, 500), new(0, 100)])]);
        var mine = new GestureLibrary([upStock]);
        var existingClose = new Command(CommandId.New(), "Existing Close", Trigger.ForGesture(upStock.Id), IsActive: true, []);
        var mapping = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal with { Commands = [existingClose] }], []));
        var vm = new ImportViewModel(mine, mapping, new RecordingEventLog());

        vm.Load(Fixture);
        Assert.Contains("2 command(s) will be added; 1 command(s) already exist and are skipped", vm.PlanText, StringComparison.Ordinal);

        vm.Conflicts.Single(c => c.Name == "Synthetic Up Stock").Choice = MergeChoice.KeepBoth;
        Assert.Contains("3 command(s) will be added; 0 command(s) already exist and are skipped", vm.PlanText, StringComparison.Ordinal);

        vm.ApplyCommand.Execute(null);
        var imported = mine.All.Single(g => g.Name == "Synthetic Up Stock (2)");
        Assert.Equal(Trigger.ForGesture(imported.Id), mapping.Global.Commands.Single(c => c.Name == "Synthetic Close Window").Trigger);
        Assert.Equal(Trigger.ForGesture(upStock.Id), mapping.Global.Commands.Single(c => c.Name == "Existing Close").Trigger);
    }

    [Fact]
    public void ApplyWritesTheFullFixtureIntoBothStores()
    {
        var mine = new GestureLibrary();
        var mapping = new MappingStore();
        var log = new RecordingEventLog();
        var vm = new ImportViewModel(mine, mapping, log);

        vm.Load(FullFixture);
        Assert.Equal("Found 11 gestures, 4 app groups, 27 commands (6 steps as placeholders until their step types exist), 2 excluded apps.", vm.StatsText);
        Assert.Empty(vm.Conflicts);
        vm.ApplyCommand.Execute(null);

        Assert.Null(vm.Error);
        Assert.Equal(11, mine.All.Count);
        Assert.Equal(5, mapping.Current.Groups.Count);
        Assert.Equal(16, mapping.Global.Commands.Count);
        Assert.Equal(2, mapping.Current.Ignored.Count);
        var entry = Assert.Single(log.Events, e => e.Source == ImportViewModel.LogSource);
        Assert.Contains(entry.Properties!, p => p.Key == "groupsAdded" && Equals(p.Value, 4));
        Assert.Contains(entry.Properties!, p => p.Key == "commandsAdded" && Equals(p.Value, 27));
        Assert.Contains(entry.Properties!, p => p.Key == "ignoredAdded" && Equals(p.Value, 2));
        Assert.Contains(entry.Properties!, p => p.Key == "placeholderSteps" && Equals(p.Value, 6));
    }

    [Fact]
    public void UnreadableFileBecomesAnErrorNotAnException()
    {
        var vm = new ImportViewModel(new GestureLibrary(), new MappingStore(), new RecordingEventLog());

        vm.Load(Path.Combine(Path.GetTempPath(), "augram-missing-" + Guid.NewGuid().ToString("N") + ".json"));

        Assert.NotNull(vm.Error);
        Assert.False(vm.CanApply);
    }

    private static Gesture Existing(string name)
        => new(GestureId.New(), name, IsActive: true, [new GestureSample([new(0, 0), new(0, 100)])]);
}
