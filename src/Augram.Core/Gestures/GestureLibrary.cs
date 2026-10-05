using Augram.Core.State;

namespace Augram.Core.Gestures;

/// <summary>
/// The single mutable owner of the gesture list for the running app (ADR-0002 §5a). Every
/// mutation validates through <see cref="GestureRules"/>, records the previous list for
/// <see cref="Undo"/>, replaces the list wholesale (records are never edited in place) and
/// raises <see cref="Changed"/>. Single-writer: the UI thread mutates; the engine reads a
/// snapshot of <see cref="All"/> when it is told the version moved.
/// </summary>
public sealed class GestureLibrary
{
    private readonly UndoStack<Gesture[]> _history = new();
    private Gesture[] _gestures;

    public GestureLibrary()
        : this([])
    {
    }

    /// <exception cref="GestureValidationException">The initial set breaks a rule; see <see cref="GestureRules.ValidSet"/>.</exception>
    public GestureLibrary(IEnumerable<Gesture> gestures)
    {
        _gestures = GestureRules.ValidSet(gestures);
    }

    /// <summary>An immutable snapshot; a new instance after every change.</summary>
    public IReadOnlyList<Gesture> All => _gestures;

    /// <summary>Increments on every change, undo and redo included.</summary>
    public int Version { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public event EventHandler? Changed;

    public Gesture? Find(GestureId id) => Array.Find(_gestures, gesture => gesture.Id == id);

    /// <summary>Appends; a gesture without samples is stored as an inactive placeholder.</summary>
    public Gesture Add(Gesture gesture)
    {
        var stored = GestureRules.Normalised(gesture);
        GestureRules.EnsureValid(stored, _gestures);
        Commit([.. _gestures, stored]);
        return stored;
    }

    /// <summary>Replaces the gesture with the same id, keeping its position.</summary>
    public Gesture Update(Gesture gesture)
    {
        var stored = GestureRules.Normalised(gesture);
        int index = IndexOf(stored.Id);
        GestureRules.EnsureValid(stored, _gestures.Where((_, i) => i != index));
        var next = (Gesture[])_gestures.Clone();
        next[index] = stored;
        Commit(next);
        return stored;
    }

    public Gesture Rename(GestureId id, string name) => Update(Require(id) with { Name = name });

    /// <exception cref="GestureValidationException">Activating a placeholder that has no samples.</exception>
    public Gesture SetActive(GestureId id, bool isActive)
    {
        var gesture = Require(id);
        if (isActive && gesture.Samples.Count == 0)
        {
            throw new GestureValidationException($"'{gesture.Name}' has no samples yet and cannot be active.");
        }

        return Update(gesture with { IsActive = isActive });
    }

    /// <summary>Removes and returns the gesture; <see cref="Undo"/> brings it back with the same id.</summary>
    public Gesture Remove(GestureId id)
    {
        int index = IndexOf(id);
        var removed = _gestures[index];
        Commit([.. _gestures[..index], .. _gestures[(index + 1)..]]);
        return removed;
    }

    /// <summary>Replaces the whole list (import). One undo step.</summary>
    public IReadOnlyList<Gesture> ReplaceAll(IEnumerable<Gesture> gestures)
    {
        Commit(GestureRules.ValidSet(gestures));
        return All;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        _gestures = _history.Undo(_gestures);
        Bump();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        _gestures = _history.Redo(_gestures);
        Bump();
        return true;
    }

    /// <summary>Forgets the undo history, e.g. after a load that should not be undoable.</summary>
    public void ClearHistory() => _history.Clear();

    private Gesture Require(GestureId id) => _gestures[IndexOf(id)];

    private int IndexOf(GestureId id)
    {
        int index = Array.FindIndex(_gestures, gesture => gesture.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"No gesture with id {id}.");
    }

    private void Commit(Gesture[] next)
    {
        _history.Record(_gestures);
        _gestures = next;
        Bump();
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
