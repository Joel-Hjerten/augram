using Augram.App.Components.GestureGrid;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Gestures;

public sealed class GesturesViewModelTests
{
    [AvaloniaFact]
    public void ProjectsTheLibrarySortedByNameAndFollowsChanges()
    {
        var (vm, library, _, _) = Create();

        Assert.Equal(library.All.Count, vm.Tiles.Count);
        Assert.Equal(vm.Tiles.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase), vm.Tiles.Select(t => t.Name));
        Assert.False(vm.CanUndo);

        library.Add(new Gesture(GestureId.New(), "Aardvark", IsActive: true, [new GestureSample([new(0, 0), new(10, 10)])]));

        Assert.Equal("Aardvark", vm.Tiles[0].Name);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public void RenameGoesThroughTheLibraryAndRejectsDuplicatesWithTheRuleMessage()
    {
        var (vm, library, _, _) = Create();
        var up = vm.Tiles.Single(t => t.Name == "Up");

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Rename, up, "down"));

        Assert.Equal("A gesture named 'Down' already exists.", vm.Message);
        Assert.Equal("Up", library.Find(up.Id)!.Name);

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Rename, up, "North"));

        Assert.Null(vm.Message);
        Assert.Equal("North", library.Find(up.Id)!.Name);
    }

    [AvaloniaFact]
    public void KeepThisDeletesTheExactDuplicatesInOneUndoStep()
    {
        var (vm, library, _, _) = Create();
        var up = library.All.Single(g => g.Name == "Up");
        var twinA = library.Add(new Gesture(GestureId.New(), "Up twin A", IsActive: true, [up.Samples[0]]));
        var twinB = library.Add(new Gesture(GestureId.New(), "Up twin B", IsActive: true, [up.Samples[0]]));
        vm.RefreshDiagnostic();
        var count = library.All.Count;

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.KeepThis, vm.Tiles.Single(t => t.Name == "Up")));

        Assert.NotNull(library.Find(up.Id));
        Assert.Null(library.Find(twinA.Id));
        Assert.Null(library.Find(twinB.Id));
        Assert.Equal(count - 2, library.All.Count);
        Assert.StartsWith("Kept 'Up'; deleted 'Up twin A', 'Up twin B'.", vm.Message, StringComparison.Ordinal);

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Undo, null));

        Assert.Equal(count, library.All.Count);
        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.KeepThis, vm.Tiles.Single(t => t.Name == "Z")));
        Assert.Equal("'Z' has no exact duplicates.", vm.Message);
    }

    [AvaloniaFact]
    public void DeleteRemovesWithoutAskingAndUndoRestores()
    {
        var (vm, library, _, _) = Create();
        var count = library.All.Count;
        var circle = vm.Tiles.Single(t => t.Name == "Circle");

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Delete, circle));

        Assert.Null(library.Find(circle.Id));
        Assert.Contains("Deleted 'Circle'", vm.Message, StringComparison.Ordinal);
        Assert.Equal(count - 1, vm.Tiles.Count);

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Undo, null));

        Assert.NotNull(library.Find(circle.Id));
        Assert.Equal(count, vm.Tiles.Count);
        Assert.True(vm.CanRedo);
    }

    [AvaloniaFact]
    public void NewRedrawAndImportReachThePresenters()
    {
        var (vm, library, training, import) = Create();
        var z = vm.Tiles.Single(t => t.Name == "Z");

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.New, null));
        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Redraw, z));
        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Import, null));

        Assert.Equal([null, z.Id], training.Requests.Select(r => r.GestureId));
        Assert.Equal(1, import.Opened);
    }

    [AvaloniaFact]
    public void ConfusionPairsOutlineExactDuplicatesOnTheirTiles()
    {
        var (vm, library, _, _) = Create();
        var before = vm.ConfusionPairs.Count;

        var up = library.All.Single(g => g.Name == "Up");
        library.Add(new Gesture(GestureId.New(), "Up twin", IsActive: true, [up.Samples[0]]));
        vm.RefreshDiagnostic();

        Assert.Equal(before + 1, vm.ConfusionPairs.Count);
        var pair = vm.ConfusionPairs.Single(p => p.SecondName == "Up twin" || p.FirstName == "Up twin");
        Assert.Equal(new HashSet<string> { "Up", "Up twin" }, new HashSet<string> { pair.FirstName, pair.SecondName });
        var twin = vm.Tiles.Single(t => t.Name == "Up twin");
        Assert.Equal(DuplicateTier.Exact, twin.Tier);
        Assert.Equal("Up", Assert.Single(twin.Partners).Name);
        Assert.Equal(DuplicateTier.Exact, vm.Tiles.Single(t => t.Name == "Up").Tier);
        Assert.Equal(DuplicateTier.None, vm.Tiles.Single(t => t.Name == "Z").Tier);
    }

    private static (GesturesViewModel Vm, GestureLibrary Library, FakeTrainingPresenter Training, FakeImportPresenter Import) Create()
    {
        var library = new GestureLibrary(StarterGestures.All());
        var training = new FakeTrainingPresenter();
        var import = new FakeImportPresenter();
        return (new GesturesViewModel(library, () => RecognitionOptions.Default, training, import), library, training, import);
    }
}
