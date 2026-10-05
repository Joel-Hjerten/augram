using Augram.App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>What the main window binds: the tab registry and which tab to open first (<c>--gallery</c>).</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(NavigationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
    }

    public NavigationRegistry Registry { get; }

    public string? InitialTabKey { get; set => SetProperty(ref field, value); }
}
