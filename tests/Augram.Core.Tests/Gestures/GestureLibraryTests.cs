using Augram.Core.Gestures;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Gestures;

public sealed class GestureLibraryTests
{
    private static readonly GesturePoint[] Up = StockFlicks.Template("Up");
    private static readonly GesturePoint[] Down = StockFlicks.Template("Down");

    [Fact]
    public void AddStoresTrimmedNameAndRaisesChanged()
    {
        var library = new GestureLibrary();
        int raised = 0;
        library.Changed += (_, _) => raised++;

        var stored = library.Add(TestGestures.Create("  Up  ", Up));

        Assert.Equal("Up", stored.Name);
        Assert.Same(stored, library.Find(stored.Id));
        Assert.Equal(1, raised);
        Assert.Equal(1, library.Version);
        Assert.True(library.CanUndo);
    }

    [Theory]
    [InlineData("Up")]
    [InlineData("up")]
    [InlineData(" UP ")]
    public void NamesMustBeUniqueIgnoringCaseAndWhitespace(string duplicate)
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up)]);

        var ex = Assert.Throws<GestureValidationException>(() => library.Add(TestGestures.Create(duplicate, Down)));

        Assert.Contains("'Up' already exists", ex.Message, StringComparison.Ordinal);
        Assert.Single(library.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyNamesAreRejected(string name)
    {
        var library = new GestureLibrary();

        Assert.Throws<GestureValidationException>(() => library.Add(TestGestures.Create(name, Up)));
        Assert.Empty(library.All);
    }

    [Fact]
    public void AGestureWithSamplesNeedsOneWithTwoDistinctPoints()
    {
        var library = new GestureLibrary();
        var degenerate = TestGestures.Create("Dot", [new GesturePoint(1, 1), new GesturePoint(1, 1)]);

        Assert.Throws<GestureValidationException>(() => library.Add(degenerate));

        var mixed = TestGestures.Create("Mixed", [new GesturePoint(1, 1)], Up);
        Assert.Equal("Mixed", library.Add(mixed).Name);
    }

    [Fact]
    public void APlaceholderWithoutSamplesIsStoredInactiveAndCannotBeActivated()
    {
        var library = new GestureLibrary();

        var placeholder = library.Add(new Gesture(GestureId.New(), "Later", IsActive: true, []));

        Assert.False(placeholder.IsActive);
        Assert.Throws<GestureValidationException>(() => library.SetActive(placeholder.Id, true));
        Assert.False(library.Find(placeholder.Id)!.IsActive);
    }

    [Fact]
    public void RenameKeepsIdPositionAndSamples()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up), TestGestures.Create("Down", Down)]);
        var id = library.All[0].Id;

        var renamed = library.Rename(id, " Flick up ");

        Assert.Equal("Flick up", renamed.Name);
        Assert.Equal(id, library.All[0].Id);
        Assert.Equal(Up, library.All[0].Samples[0].ToArray());
        Assert.Throws<GestureValidationException>(() => library.Rename(id, "down"));
        Assert.Equal("flick UP", library.Rename(id, "flick UP").Name);
    }

    [Fact]
    public void UpdateReplacesByIdAndRejectsUnknownIds()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up), TestGestures.Create("Down", Down)]);
        var original = library.All[1];

        var updated = library.Update(original with { Samples = [new GestureSample(Up), new GestureSample(Down)] });

        Assert.Equal(2, library.All[1].Samples.Count);
        Assert.Same(updated, library.All[1]);
        Assert.Throws<KeyNotFoundException>(() => library.Update(original with { Id = GestureId.New() }));
    }

    [Fact]
    public void SetActiveTogglesAndIsUndoable()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up)]);
        var id = library.All[0].Id;

        Assert.False(library.SetActive(id, false).IsActive);
        Assert.True(library.Undo());
        Assert.True(library.Find(id)!.IsActive);
    }

    [Fact]
    public void RemoveThenUndoRestoresTheSameIdInTheSamePlace()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up), TestGestures.Create("Down", Down)]);
        var id = library.All[0].Id;

        var removed = library.Remove(id);

        Assert.Equal("Up", removed.Name);
        Assert.Null(library.Find(id));
        Assert.Equal(["Down"], library.All.Select(gesture => gesture.Name));

        Assert.True(library.Undo());

        Assert.Equal(id, library.All[0].Id);
        Assert.Equal(["Up", "Down"], library.All.Select(gesture => gesture.Name));
        Assert.Throws<KeyNotFoundException>(() => library.Remove(GestureId.New()));
    }

    [Fact]
    public void UndoRedoSequenceWalksBothWaysAndANewChangeDropsRedo()
    {
        var library = new GestureLibrary();
        var up = library.Add(TestGestures.Create("Up", Up));
        library.Add(TestGestures.Create("Down", Down));
        library.Rename(up.Id, "North");

        Assert.True(library.Undo());
        Assert.Equal("Up", library.Find(up.Id)!.Name);
        Assert.True(library.Undo());
        Assert.Single(library.All);
        Assert.True(library.Redo());
        Assert.Equal(2, library.All.Count);
        Assert.True(library.CanRedo);

        library.SetActive(up.Id, false);

        Assert.False(library.CanRedo);
        Assert.False(library.Redo());
        Assert.Equal(7, library.Version);
    }

    [Fact]
    public void ReplaceAllIsOneUndoStepAndValidatesTheWholeSet()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up)]);
        var imported = new[] { TestGestures.Create("A", Up), TestGestures.Create("B", Down) };

        var all = library.ReplaceAll(imported);

        Assert.Equal(["A", "B"], all.Select(gesture => gesture.Name));
        Assert.Throws<GestureValidationException>(() => library.ReplaceAll([TestGestures.Create("X", Up), TestGestures.Create("x", Down)]));
        Assert.Equal(["A", "B"], library.All.Select(gesture => gesture.Name));
        Assert.True(library.Undo());
        Assert.Equal(["Up"], library.All.Select(gesture => gesture.Name));
    }

    [Fact]
    public void AllIsASnapshotThatDoesNotChangeUnderTheCaller()
    {
        var library = new GestureLibrary([TestGestures.Create("Up", Up)]);
        var before = library.All;

        library.Add(TestGestures.Create("Down", Down));

        Assert.Single(before);
        Assert.Equal(2, library.All.Count);
    }

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        var library = new GestureLibrary();
        var up = library.Add(TestGestures.Create("Up", Up));

        Assert.Throws<GestureValidationException>(() => library.Add(up with { Name = "Other" }));
    }
}
