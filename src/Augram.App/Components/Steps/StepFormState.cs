using System.Runtime.CompilerServices;
using Augram.App.Declarations;

namespace Augram.App.Components.Steps;

/// <summary>
/// The one value a step's form edits (the step, or the part of it the form edits, such as a Remap step's output) and the one way
/// an edit leaves the form: <see cref="Emit"/> normalises the new value (when the form gives a rule), drops an edit that changes
/// nothing (no undo step for it), keeps it as <see cref="Current"/>, tells <see cref="Accepted"/> (state that follows the value: a
/// remembered choice, a capture field), hands it to the host, then refreshes the editors registered with <see cref="Refreshed"/>
/// or <see cref="OnEdit"/>. A form builds its fields' bindings here: <see cref="Bind"/> for an editor that shows only what it set
/// itself, <see cref="Refreshed"/> for one another editor can change (a dropdown beside a capture field).
/// </summary>
public sealed class StepFormState<TStep>
    where TStep : notnull
{
    private readonly Action<TStep> _changed;
    private readonly Func<TStep, TStep>? _normalise;
    private readonly List<Action> _refresh = [];

    /// <param name="current">The value the form opens on.</param>
    /// <param name="changed">The host's callback; called once per edit that changes the value.</param>
    /// <param name="normalise">The form's rule for a canonical value (a hotkey's right-hand bits only on modifiers that are on); none by default.</param>
    public StepFormState(TStep current, Action<TStep> changed, Func<TStep, TStep>? normalise = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(changed);
        Current = current;
        _changed = changed;
        _normalise = normalise;
    }

    /// <summary>The value as last emitted (or as opened).</summary>
    public TStep Current { get; private set; }

    /// <summary>Called with an accepted value before the host hears of it.</summary>
    public Action<TStep>? Accepted { get; init; }

    /// <summary>
    /// A field's binding over <paramref name="get"/> and <paramref name="set"/>. Its property name, for the F1 inspector, is
    /// <paramref name="propertyName"/> or read from the getter (<c>() => form.Current.Milliseconds</c> is <c>Milliseconds</c>).
    /// </summary>
    public DelegateBinding<T> Bind<T>(Func<T> get, Action<T> set, string? propertyName = null, [CallerArgumentExpression(nameof(get))] string getterExpression = "")
        => new(get, set, propertyName: propertyName, getterExpression: getterExpression);

    /// <summary>A binding whose editor is refreshed after every edit, as <see cref="Bind"/> otherwise.</summary>
    public DelegateBinding<T> Refreshed<T>(Func<T> get, Action<T> set, string? propertyName = null, [CallerArgumentExpression(nameof(get))] string getterExpression = "")
    {
        var binding = Bind(get, set, propertyName, getterExpression);
        _refresh.Add(binding.NotifyChanged);
        return binding;
    }

    /// <summary>Runs <paramref name="refresh"/> after every edit, after the bindings registered before it; for an editor that is not a binding (a toggle, a capture field).</summary>
    public void OnEdit(Action refresh)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        _refresh.Add(refresh);
    }

    /// <summary>The one way an edit leaves the form; an edit that changes nothing emits nothing.</summary>
    public void Emit(TStep next)
    {
        if (_normalise is not null)
        {
            next = _normalise(next);
        }

        if (EqualityComparer<TStep>.Default.Equals(next, Current))
        {
            return;
        }

        Current = next;
        Accepted?.Invoke(next);
        _changed(next);
        foreach (var refresh in _refresh)
        {
            refresh();
        }
    }
}
