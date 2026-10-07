using Augram.App.Components.GestureGrid;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Gestures;

public sealed class GesturesViewModelTests
{
    [AvaloniaFact]
    public void ProjectsTheLibrarySortedByNameAndFollowsChanges()
    {
        var f = Create();

        Assert.Equal(f.Library.All.Count, f.Vm.Tiles.Count);
        Assert.Equal(f.Vm.Tiles.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase), f.Vm.Tiles.Select(t => t.Name));
        Assert.False(f.Vm.CanUndo);

        f.Library.Add(new Gesture(GestureId.New(), "Aardvark", IsActive: true, [new GestureSample([new(0, 0), new(10, 10)])]));

        Assert.Equal("Aardvark", f.Vm.Tiles[0].Name);
        Assert.True(f.Vm.CanUndo);
    }

    [AvaloniaFact]
    public void RenameGoesThroughTheLibraryAndRejectsDuplicatesWithTheRuleMessage()
    {
        var f = Create();
        var up = f.Vm.Tiles.Single(t => t.Name == "Up");

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Rename, up, "down"));

        Assert.Equal("A gesture named 'Down' already exists.", f.Vm.Message);
        Assert.Equal("Up", f.Library.Find(up.Id)!.Name);

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Rename, up, "North"));

        Assert.Null(f.Vm.Message);
        Assert.Equal("North", f.Library.Find(up.Id)!.Name);
    }

    [AvaloniaFact]
    public void KeepThisDeletesTheExactDuplicatesInOneUndoStep()
    {
        var f = Create();
        var up = f.Library.All.Single(g => g.Name == "Up");
        var twinA = f.Library.Add(new Gesture(GestureId.New(), "Up twin A", IsActive: true, [up.Samples[0]]));
        var twinB = f.Library.Add(new Gesture(GestureId.New(), "Up twin B", IsActive: true, [up.Samples[0]]));
        f.Vm.RefreshDiagnostic();
        var count = f.Library.All.Count;

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.KeepThis, f.Vm.Tiles.Single(t => t.Name == "Up")));

        Assert.NotNull(f.Library.Find(up.Id));
        Assert.Null(f.Library.Find(twinA.Id));
        Assert.Null(f.Library.Find(twinB.Id));
        Assert.Equal(count - 2, f.Library.All.Count);
        Assert.StartsWith("Kept 'Up'; deleted 'Up twin A', 'Up twin B'.", f.Vm.Message, StringComparison.Ordinal);

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Undo, null));

        Assert.Equal(count, f.Library.All.Count);
        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.KeepThis, f.Vm.Tiles.Single(t => t.Name == "Z")));
        Assert.Equal("'Z' has no duplicates.", f.Vm.Message);
    }

    [AvaloniaFact]
    public void KeepThisRetargetsTheDuplicatesCommandsToTheKeptGestureOrUnbindsOnAClash()
    {
        var f = Create();
        var up = f.Library.All.Single(g => g.Name == "Up");
        var twin = f.Library.Add(new Gesture(GestureId.New(), "Up twin", IsActive: true, [up.Samples[0]]));
        f.Vm.RefreshDiagnostic();
        // Chrome binds only the twin: retargeted. Global binds both: the twin's command cannot move to Up (one command per trigger per group) and is unbound.
        var chromeClose = f.Mapping.AddGroup(MappingFixture.Group("Chrome", MappingFixture.Command("Close tab", twin.Id))).Commands[0];
        f.Mapping.AddCommand(GroupId.Global, MappingFixture.Command("Minimize", up.Id));
        var globalTwin = f.Mapping.AddCommand(GroupId.Global, MappingFixture.Command("Minimize twin", twin.Id));

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.KeepThis, f.Vm.Tiles.Single(t => t.Name == "Up")));

        Assert.Null(f.Library.Find(twin.Id));
        Assert.Equal(Trigger.ForGesture(up.Id), f.Mapping.FindCommand(chromeClose.Id)!.Value.Command.Trigger);
        Assert.Equal(Trigger.None, f.Mapping.FindCommand(globalTwin.Id)!.Value.Command.Trigger);
        Assert.Empty(f.Mapping.UsedBy(twin.Id));
        Assert.Equal($"Kept 'Up'; deleted 'Up twin'. 1 command retargeted to 'Up'; 1 command unbound (its group already used 'Up'). {GestureGridKeymap.Undo} undoes the delete; the Commands tab undoes the retargeting.", f.Vm.Message);
    }

    [AvaloniaFact]
    public void DeleteOfAnUnusedGestureRemovesWithoutAskingAndUndoRestores()
    {
        var f = Create();
        var count = f.Library.All.Count;
        var circle = f.Vm.Tiles.Single(t => t.Name == "Circle");

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Delete, circle));

        Assert.Empty(f.Confirm.Requests);
        Assert.Null(f.Library.Find(circle.Id));
        Assert.Contains("Deleted 'Circle'", f.Vm.Message, StringComparison.Ordinal);
        Assert.Equal(count - 1, f.Vm.Tiles.Count);

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Undo, null));

        Assert.NotNull(f.Library.Find(circle.Id));
        Assert.Equal(count, f.Vm.Tiles.Count);
        Assert.True(f.Vm.CanRedo);
    }

    [AvaloniaFact]
    public void DeleteOfAUsedGestureAsksNamingTheCommandsThenUnbindsThemAndRemoves()
    {
        var f = Create();
        var down = f.Vm.Tiles.Single(t => t.Name == "Down");
        var minimize = f.Mapping.AddCommand(GroupId.Global, MappingFixture.Command("Minimize", down.Id));
        var closeTab = f.Mapping.AddGroup(MappingFixture.Group("Chrome", MappingFixture.Command("Close tab", down.Id))).Commands[0];

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Delete, down));

        var asked = Assert.Single(f.Confirm.Requests);
        Assert.Equal("Delete gesture", asked.Title);
        Assert.Equal("Delete", asked.ConfirmLabel);
        Assert.Equal("Delete 'Down'? It is used by Global › Minimize, Chrome › Close tab. Those commands lose their gesture.", asked.Message);
        Assert.Null(f.Library.Find(down.Id));
        Assert.Equal(Trigger.None, f.Mapping.FindCommand(minimize.Id)!.Value.Command.Trigger);
        Assert.Equal(Trigger.None, f.Mapping.FindCommand(closeTab.Id)!.Value.Command.Trigger);
        Assert.Equal("Minimize", f.Mapping.FindCommand(minimize.Id)!.Value.Command.Name);
        Assert.StartsWith("Deleted 'Down' and unbound 2 commands.", f.Vm.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void DeleteOfAUsedGestureChangesNothingWhenDeclined()
    {
        var f = Create();
        f.Confirm.Answer = false;
        var down = f.Vm.Tiles.Single(t => t.Name == "Down");
        var minimize = f.Mapping.AddCommand(GroupId.Global, MappingFixture.Command("Minimize", down.Id));
        var version = f.Mapping.Version;

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Delete, down));

        Assert.Single(f.Confirm.Requests);
        Assert.NotNull(f.Library.Find(down.Id));
        Assert.Equal(Trigger.ForGesture(down.Id), f.Mapping.FindCommand(minimize.Id)!.Value.Command.Trigger);
        Assert.Equal(version, f.Mapping.Version);
        Assert.False(f.Vm.CanUndo);
        Assert.Null(f.Vm.Message);
    }

    [AvaloniaFact]
    public void NewRedrawImportAndUsedByReachThePresenters()
    {
        var f = Create();
        var z = f.Vm.Tiles.Single(t => t.Name == "Z");

        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.New, null));
        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Redraw, z));
        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Import, null));
        f.Vm.Handle(new GestureGridActionEventArgs(GestureGridAction.UsedBy, z));

        Assert.Equal([null, z.Id], f.Training.Requests.Select(r => r.GestureId));
        Assert.Equal(1, f.Import.Opened);
        Assert.Equal([z.Id], f.UsedBy.Shown);
    }

    [AvaloniaFact]
    public void DuplicatesAreFoundAtTheDuplicateCutOffAndMarkTheirTiles()
    {
        var f = Create();
        var before = f.Vm.ConfusionPairs.Count;

        var up = f.Library.All.Single(g => g.Name == "Up");
        f.Library.Add(new Gesture(GestureId.New(), "Up twin", IsActive: true, [up.Samples[0]]));
        f.Vm.RefreshDiagnostic();

        Assert.Equal(before + 1, f.Vm.ConfusionPairs.Count);
        var pair = f.Vm.ConfusionPairs.Single(p => p.SecondName == "Up twin" || p.FirstName == "Up twin");
        Assert.Equal(new HashSet<string> { "Up", "Up twin" }, new HashSet<string> { pair.FirstName, pair.SecondName });
        var twin = f.Vm.Tiles.Single(t => t.Name == "Up twin");
        Assert.True(twin.HasDuplicates);
        Assert.Equal("Up", Assert.Single(twin.Partners).Name);
        Assert.True(f.Vm.Tiles.Single(t => t.Name == "Up").HasDuplicates);
        Assert.False(f.Vm.Tiles.Single(t => t.Name == "Z").HasDuplicates);
        Assert.All(f.Vm.ConfusionPairs, pair => Assert.True(pair.Score >= ConfusionCheck.DuplicateCutOff));
    }

    private static Fixture Create()
    {
        var library = new GestureLibrary(StarterGestures.All());
        var mapping = new MappingStore();
        var training = new FakeTrainingPresenter();
        var import = new FakeImportPresenter();
        var usedBy = new FakeUsedByPresenter();
        var confirm = new FakeConfirmPresenter();
        var vm = new GesturesViewModel(library, () => RecognitionOptions.Default, training, import, mapping, usedBy, confirm);
        return new Fixture(vm, library, mapping, training, import, usedBy, confirm);
    }

    private sealed record Fixture(
        GesturesViewModel Vm,
        GestureLibrary Library,
        MappingStore Mapping,
        FakeTrainingPresenter Training,
        FakeImportPresenter Import,
        FakeUsedByPresenter UsedBy,
        FakeConfirmPresenter Confirm);
}
