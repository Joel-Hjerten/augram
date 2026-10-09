using System.ComponentModel;
using Augram.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// Which platform's fields the identification form shows (Joel, 2026-10-09: one layout, flipped between Windows and macOS
/// by a switch at the top). UI state only, kept apart from <see cref="AppMatcherEditViewModel"/> so a flip is never an edit
/// the host applies. Opens on this machine's platform.
/// </summary>
public sealed partial class AppMatcherFormView : ObservableObject
{
    public AppMatcherFormView(HostPlatform shown)
    {
        Shown = shown;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWindows))]
    public partial HostPlatform Shown { get; set; }

    public bool ShowsWindows => Shown == HostPlatform.Windows;
}

/// <summary>Raises a nameless change whenever any of its sources changes: the owner of a binding that reads the form and the view together.</summary>
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
