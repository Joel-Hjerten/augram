using Augram.App.Hosting;
using Augram.App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>What the main window binds: its title ("Augram (Dev)" for a development build), the tab registry and which tab to open first (<c>--gallery</c>).</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <param name="registry">The tabs.</param>
    /// <param name="app">This build; <see cref="AppInfo.Current"/> when null.</param>
    public MainWindowViewModel(NavigationRegistry registry, AppInfo? app = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        Title = (app ?? AppInfo.Current).DisplayName;
    }

    /// <summary>"Augram" for the installed build, "Augram (Dev)" otherwise, so there is no mistaking which one runs (Joel, 2026-10-08).</summary>
    public string Title { get; }

    public NavigationRegistry Registry { get; }

    public string? InitialTabKey { get; set => SetProperty(ref field, value); }
}
