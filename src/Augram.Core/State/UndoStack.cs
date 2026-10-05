namespace Augram.Core.State;

/// <summary>
/// Linear undo/redo history over immutable snapshots. A store records the snapshot it is
/// about to replace, then <see cref="Undo"/> hands it back in exchange for the current one.
/// Recording after an undo discards the redo branch (standard editor semantics). Oldest
/// entries fall off past <see cref="Capacity"/>. Not thread-safe: the owning store is
/// single-writer.
/// </summary>
/// <typeparam name="T">An immutable snapshot (a record or a copied array).</typeparam>
public sealed class UndoStack<T>
{
    public const int DefaultCapacity = 100;

    private readonly List<T> _undo = [];
    private readonly List<T> _redo = [];

    public UndoStack(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Remembers the state being replaced. Clears the redo branch.</summary>
    public void Record(T snapshot)
    {
        Push(_undo, snapshot);
        _redo.Clear();
    }

    /// <summary>Returns the previous snapshot and keeps <paramref name="current"/> for <see cref="Redo"/>.</summary>
    public T Undo(T current)
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("Nothing to undo.");
        }

        var snapshot = Pop(_undo);
        Push(_redo, current);
        return snapshot;
    }

    /// <summary>Returns the snapshot undone last and keeps <paramref name="current"/> for <see cref="Undo"/>.</summary>
    public T Redo(T current)
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Nothing to redo.");
        }

        var snapshot = Pop(_redo);
        Push(_undo, current);
        return snapshot;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void Push(List<T> stack, T snapshot)
    {
        stack.Add(snapshot);
        if (stack.Count > Capacity)
        {
            stack.RemoveAt(0);
        }
    }

    private static T Pop(List<T> stack)
    {
        var snapshot = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return snapshot;
    }
}
