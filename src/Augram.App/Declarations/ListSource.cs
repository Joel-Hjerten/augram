namespace Augram.App.Declarations;

/// <summary>An <see cref="IListSource"/> over a snapshot function; the owner calls <see cref="NotifyChanged"/> after it changes the data.</summary>
public sealed class ListSource<T> : IListSource
    where T : class
{
    private readonly Func<IReadOnlyList<T>> _items;

    public ListSource(Func<IReadOnlyList<T>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<object> Items => _items();

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
