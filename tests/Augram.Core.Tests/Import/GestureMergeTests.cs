using Augram.Core.Gestures;
using Augram.Core.Tests.Fixtures;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

public sealed class GestureMergeTests
{
    private static readonly GesturePoint[] Line = [new(0, 0), new(0, 100)];
    private static readonly GesturePoint[] OtherLine = [new(0, 0), new(100, 0)];

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

        Assert.Equal(["Up", "Left", "up (imported)", "Circle"], merged.Select(gesture => gesture.Name));
        Assert.Equal([MineUp.Id, MineLeft.Id, TheirsUp.Id, TheirsCircle.Id], merged.Select(gesture => gesture.Id));
    }

    [Fact]
    public void KeepBothAvoidsASecondClash()
    {
        var taken = TestGestures.Create("Up (imported)", Line);
        var plan = GestureMerge.Plan([MineUp, taken], [TheirsUp]);

        var merged = GestureMerge.Apply(plan, new Dictionary<GestureId, MergeChoice>(), defaultChoice: MergeChoice.KeepBoth);

        Assert.Equal(["Up", "Up (imported)", "up (imported) 2"], merged.Select(gesture => gesture.Name));
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
}
