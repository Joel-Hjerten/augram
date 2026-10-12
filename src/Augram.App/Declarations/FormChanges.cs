using System.ComponentModel;

namespace Augram.App.Declarations;

/// <summary>
/// Raises a nameless change whenever any of its sources changes: the owner of a <see cref="DelegateBinding{T}"/> that reads
/// several observables together (the identification form's fields and the Windows/macOS view that picks which ones show).
/// </summary>
internal sealed class FormChanges : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs Any = new(string.Empty);

    public FormChanges(params INotifyPropertyChanged[] sources)
    {
        foreach (var source in sources)
        {
            source.PropertyChanged += (_, _) => PropertyChanged?.Invoke(this, Any);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
