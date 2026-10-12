using System.Collections;
using System.ComponentModel;
using Augram.App.Declarations;

namespace Augram.App.ViewModels;

/// <summary>
/// The ticks of a check-list dialog (<see cref="CheckListField"/>): the Not in, Also in, Allowed for and export dialogs. It
/// owns the ticked ids, calls <c>changed</c> when a tick really changes, and builds each <see cref="CheckListItem"/> with its
/// bool binding on the dialog's view model (raised under the property name the dialog gives). The dialog keeps what it
/// offers, its order and its form.
/// </summary>
public sealed class CheckedSet<TId> : IReadOnlyCollection<TId>
    where TId : notnull
{
    private readonly HashSet<TId> _checked;
    private readonly Action _changed;

    /// <param name="ticked">The ids ticked at the start.</param>
    /// <param name="changed">Called after a tick or an untick that changed the set (the dialog raises its property, or recounts).</param>
    public CheckedSet(IEnumerable<TId> ticked, Action changed)
    {
        ArgumentNullException.ThrowIfNull(ticked);
        ArgumentNullException.ThrowIfNull(changed);
        _checked = [.. ticked];
        _changed = changed;
    }

    public int Count => _checked.Count;

    public bool IsChecked(TId id) => _checked.Contains(id);

    public void SetChecked(TId id, bool value)
    {
        if (value ? _checked.Add(id) : _checked.Remove(id))
        {
            _changed();
        }
    }

    /// <summary>A box for <paramref name="id"/>: its caption, its tick bound here and re-read when <paramref name="owner"/> raises <paramref name="propertyName"/>, and its detail.</summary>
    public CheckListItem Item(string caption, TId id, INotifyPropertyChanged owner, string propertyName, string? detail = null)
        => new(caption, new DelegateBinding<bool>(() => IsChecked(id), value => SetChecked(id, value), owner, propertyName), detail);

    public IEnumerator<TId> GetEnumerator() => _checked.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
