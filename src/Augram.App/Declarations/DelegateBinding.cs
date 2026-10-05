using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// An <see cref="IValueBinding{T}"/> over a getter and an optional setter. The property name is read
/// from the getter expression (<c>() => vm.StartDistance</c> becomes <c>StartDistance</c>) unless given.
/// When an <see cref="INotifyPropertyChanged"/> owner is given, a change of that
/// property (or of any property, when the name is unknown) raises <see cref="Changed"/>.
/// </summary>
public sealed class DelegateBinding<T> : IValueBinding<T>
{
    private readonly Func<T> _get;
    private readonly Action<T>? _set;

    public DelegateBinding(
        Func<T> get,
        Action<T>? set = null,
        INotifyPropertyChanged? owner = null,
        string? propertyName = null,
        [CallerArgumentExpression(nameof(get))] string getterExpression = "")
    {
        ArgumentNullException.ThrowIfNull(get);
        _get = get;
        _set = set;
        PropertyName = propertyName ?? PropertyNameFrom(getterExpression);
        if (owner is not null)
        {
            owner.PropertyChanged += OnOwnerPropertyChanged;
        }
    }

    public event EventHandler? Changed;

    public string? PropertyName { get; }

    public bool IsReadOnly => _set is null;

    public T Get() => _get();

    public void Set(T value) => _set?.Invoke(value);

    /// <summary>For owners that are not observable: tell renderers the value changed underneath them.</summary>
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string? PropertyNameFrom(string getterExpression)
    {
        var text = getterExpression.Trim();
        var arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow >= 0)
        {
            text = text[(arrow + 2)..].Trim();
        }

        var dot = text.LastIndexOf('.');
        if (dot >= 0)
        {
            text = text[(dot + 1)..];
        }

        return text.Length > 0 && text.All(c => char.IsLetterOrDigit(c) || c == '_') ? text : null;
    }

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (PropertyName is null || string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == PropertyName)
        {
            NotifyChanged();
        }
    }
}
