using Augram.Core.State;
using Xunit;

namespace Augram.Core.Tests.State;

public sealed class UndoStackTests
{
    [Fact]
    public void UndoReturnsSnapshotsNewestFirstAndRedoReplaysThem()
    {
        var stack = new UndoStack<string>();
        stack.Record("v0");
        stack.Record("v1");

        Assert.Equal("v1", stack.Undo("v2"));
        Assert.Equal("v0", stack.Undo("v1"));
        Assert.False(stack.CanUndo);
        Assert.Equal("v1", stack.Redo("v0"));
        Assert.Equal("v2", stack.Redo("v1"));
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void RecordingAfterUndoDropsTheRedoBranch()
    {
        var stack = new UndoStack<string>();
        stack.Record("v0");
        stack.Undo("v1");

        stack.Record("v0");

        Assert.False(stack.CanRedo);
        Assert.Throws<InvalidOperationException>(() => stack.Redo("x"));
    }

    [Fact]
    public void OldestEntriesFallOffPastTheCapacity()
    {
        var stack = new UndoStack<int>(capacity: 3);
        for (int i = 0; i < 5; i++)
        {
            stack.Record(i);
        }

        Assert.Equal(4, stack.Undo(5));
        Assert.Equal(3, stack.Undo(4));
        Assert.Equal(2, stack.Undo(3));
        Assert.False(stack.CanUndo);
        Assert.Throws<InvalidOperationException>(() => stack.Undo(2));
    }

    [Fact]
    public void DefaultCapacityIsOneHundredAndClearForgetsEverything()
    {
        var stack = new UndoStack<int>();
        Assert.Equal(100, stack.Capacity);
        stack.Record(1);
        stack.Undo(2);

        stack.Clear();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Throws<ArgumentOutOfRangeException>(() => new UndoStack<int>(0));
    }
}
