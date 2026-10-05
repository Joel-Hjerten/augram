using Augram.Core.Capture;
using Augram.Core.Recognition;
using Augram.Core.State;

namespace Augram.Core.Config;

/// <summary>
/// The single mutable owner of <see cref="Settings"/> for the running app, the same shape as
/// <c>GestureLibrary</c>: validate, record for undo, replace, raise <see cref="Changed"/>.
/// Single-writer (the UI thread); the engine reads <see cref="Current"/> as a snapshot.
/// </summary>
public sealed class SettingsStore
{
    private readonly UndoStack<Settings> _history = new();

    /// <exception cref="SettingsValidationException">A loaded value is out of range.</exception>
    public SettingsStore(Settings initial)
    {
        SettingsRules.EnsureValid(initial);
        Current = initial;
    }

    public Settings Current { get; private set; }

    /// <summary>Increments on every change, undo and redo included.</summary>
    public int Version { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public event EventHandler? Changed;

    /// <summary>Applies one edit as one undo step; the result is validated before it is committed.</summary>
    public Settings Apply(Func<Settings, Settings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var next = change(Current) ?? throw new ArgumentException("The change produced no settings.", nameof(change));
        SettingsRules.EnsureValid(next);
        _history.Record(Current);
        Current = next;
        Bump();
        return next;
    }

    public Settings SetGeneral(GeneralSettings general) => Apply(settings => settings with { General = general });

    public Settings SetCapture(CaptureThresholds capture) => Apply(settings => settings with { Capture = capture });

    public Settings SetTrail(TrailSettings trail) => Apply(settings => settings with { Trail = trail });

    public Settings SetRecognition(RecognitionOptions recognition) => Apply(settings => settings with { Recognition = recognition });

    public Settings SetNoMatch(NoMatchBehaviour noMatch) => Apply(settings => settings with { NoMatch = noMatch });

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        Current = _history.Undo(Current);
        Bump();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        Current = _history.Redo(Current);
        Bump();
        return true;
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
