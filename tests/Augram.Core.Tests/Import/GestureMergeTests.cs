using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

public sealed class GestureMergeTests
{
    private static readonly GesturePoint[] Line = [new(0, 0), new(0, 100)];
    private static readonly GesturePoint[] OtherLine = [new(0, 0), new(100, 0)];
    private static readonly GesturePoint[] LongerLine = [new(0, 0), new(0, 50), new(0, 100)];

    private static readonly Gesture MineUp = TestGestures.Create("Up", Line);
    private static readonly Gesture MineLeft = TestGestures.Create("Left", Line);
    private static readonly Gesture TheirsUp = TestGestures.Create("up", isActive: false, OtherLine);
    private static readonly Gesture TheirsCircle = TestGestures.Create("Circle", OtherLine);

    private static readonly MergePlan Plan = GestureMerge.Plan([MineUp, MineLeft], [TheirsUp, TheirsCircle]);

    [Fact]
    public void PlanClassifiesAddsAndConflictsCaseInsensitively()
    {
        Assert.Equal([MergeKind.Conflict, MergeKind.Add], Plan.Entries.Select(entry => entry.Kind));
        Assert.Same(MineUp, Plan.Entries[0].Existing);
        Assert.Null(Plan.Entries[1].Existing);
        Assert.Equal([TheirsUp], Plan.Conflicts.Select(entry => entry.Imported));
    }

    [Fact]
    public void KeepMineLeavesExistingUntouchedAndStillAdds()
    {
        var merged = GestureMerge.Apply(Plan, new Dictionary<GestureId, MergeChoice> { [TheirsUp.Id] = MergeChoice.KeepMine });

        Assert.Equal([MineUp, MineLeft, TheirsCircle], merged);
    }

    [Fact]
    public void TakeTheirsReplacesContentButKeepsExistingId()
    {
        var merged = GestureMerge.Apply(Plan, new Dictionary<GestureId, MergeChoice> { [TheirsUp.Id] = MergeChoice.TakeTheirs });

        Assert.Equal([MineUp.Id, MineLeft.Id, TheirsCircle.Id], merged.Select(gesture => gesture.Id));
        Assert.Equal("up", merged[0].Name);
        Assert.False(merged[0].IsActive);
        Assert.Same(TheirsUp.Samples, merged[0].Samples);
    }

    [Fact]
    public void KeepBothRenamesTheImportedOne()
    {
        var merged = GestureMerge.Apply(Plan, new Dictionary<GestureId, MergeChoice> { [TheirsUp.Id] = MergeChoice.KeepBoth });

        Assert.Equal(["Up", "Left", "up (2)", "Circle"], merged.Select(gesture => gesture.Name));
        Assert.Equal([MineUp.Id, MineLeft.Id, TheirsUp.Id, TheirsCircle.Id], merged.Select(gesture => gesture.Id));
    }

    [Fact]
    public void KeepBothAvoidsASecondClash()
    {
        var taken = TestGestures.Create("Up (2)", Line);
        var plan = GestureMerge.Plan([MineUp, taken], [TheirsUp]);

        var merged = GestureMerge.Apply(plan, new Dictionary<GestureId, MergeChoice>(), defaultChoice: MergeChoice.KeepBoth);

        Assert.Equal(["Up", "Up (2)", "up (3)"], merged.Select(gesture => gesture.Name));
    }

    [Fact]
    public void DefaultChoiceAppliesToConflictsWithoutAChoice()
    {
        var merged = GestureMerge.Apply(Plan, new Dictionary<GestureId, MergeChoice>(), defaultChoice: MergeChoice.TakeTheirs);

        Assert.Equal("up", merged[0].Name);
        Assert.Equal(MineUp.Id, merged[0].Id);
    }

    [Fact]
    public void PlanDoesNotMutateInputs()
    {
        Assert.Equal([MineUp, MineLeft], Plan.Existing);
        Assert.Equal("Up", MineUp.Name);
        Assert.Equal("up", TheirsUp.Name);
    }

    [Theory]
    [InlineData(MergeChoice.KeepMine, true)]
    [InlineData(MergeChoice.TakeTheirs, true)]
    [InlineData(MergeChoice.KeepBoth, false)]
    public void ApplyWithMapResolvesEveryImportedId(MergeChoice choice, bool resolvesToExisting)
    {
        var outcome = GestureMerge.ApplyWithMap(Plan, new Dictionary<GestureId, MergeChoice> { [TheirsUp.Id] = choice });

        Assert.Equal(resolvesToExisting ? MineUp.Id : TheirsUp.Id, outcome.IdMap[TheirsUp.Id]);
        Assert.Equal(TheirsCircle.Id, outcome.IdMap[TheirsCircle.Id]);
        Assert.Equal(2, outcome.IdMap.Count);
        Assert.Equal(GestureMerge.Apply(Plan, new Dictionary<GestureId, MergeChoice> { [TheirsUp.Id] = choice }), outcome.Gestures);
    }

    [Fact]
    public void ShapePlanFlagsADuplicateShapeUnderAnotherName()
    {
        var flick = TestGestures.Create("Flick", LongerLine);

        var plan = GestureMerge.Plan([MineUp, MineLeft], [flick], RecognitionOptions.Default);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(MergeKind.SameShape, entry.Kind);
        Assert.Same(MineUp, entry.Existing);
        Assert.True(entry.ShapeScore >= ConfusionCheck.DuplicateCutOff);
        Assert.Equal([entry], plan.Conflicts);
    }

    [Fact]
    public void ShapePlanLeavesDifferentShapesAsAdditions()
    {
        var plan = GestureMerge.Plan([MineUp], [TheirsCircle], RecognitionOptions.Default);

        Assert.Equal(MergeKind.Add, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void ShapePlanMatchesInactiveExistingGesturesToo()
    {
        var sleeping = TestGestures.Create("Sleeping", isActive: false, Line);

        var plan = GestureMerge.Plan([sleeping], [TestGestures.Create("Flick", LongerLine)], RecognitionOptions.Default);

        Assert.Equal(MergeKind.SameShape, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void ShapePlanKeepsNameClashesAsConflicts()
    {
        var plan = GestureMerge.Plan([MineUp], [TestGestures.Create("up", LongerLine)], RecognitionOptions.Default);

        Assert.Equal(MergeKind.Conflict, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void ShapePlanWithAnEmptyLibraryAddsEverything()
    {
        var plan = GestureMerge.Plan([], [TheirsUp, TheirsCircle], RecognitionOptions.Default);

        Assert.All(plan.Entries, entry => Assert.Equal(MergeKind.Add, entry.Kind));
    }

    [Fact]
    public void KeepMineOnAShapeMatchMapsToTheExistingGesture()
    {
        var flick = TestGestures.Create("Flick", LongerLine);
        var plan = GestureMerge.Plan([MineUp], [flick], RecognitionOptions.Default);

        var outcome = GestureMerge.ApplyWithMap(plan, new Dictionary<GestureId, MergeChoice>());

        Assert.Equal([MineUp], outcome.Gestures);
        Assert.Equal(MineUp.Id, outcome.IdMap[flick.Id]);
    }

    [Fact]
    public void KeepBothOnAShapeMatchKeepsTheImportedName()
    {
        var flick = TestGestures.Create("Flick", LongerLine);
        var plan = GestureMerge.Plan([MineUp], [flick], RecognitionOptions.Default);

        var outcome = GestureMerge.ApplyWithMap(plan, new Dictionary<GestureId, MergeChoice> { [flick.Id] = MergeChoice.KeepBoth });

        Assert.Equal(["Up", "Flick"], outcome.Gestures.Select(gesture => gesture.Name));
        Assert.Equal(flick.Id, outcome.IdMap[flick.Id]);
    }
}
