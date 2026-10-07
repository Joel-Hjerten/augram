using Augram.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Navigation;

/// <summary>
/// Switches the main window to a tab by key from anywhere (the "Used by…" jump to a command). The
/// hook is the one <c>--gallery</c> uses: <see cref="MainWindowViewModel.InitialTabKey"/>, which
/// <c>MainWindow</c> binds to <c>Shell.SelectedKey</c>. The key is cleared first so re-selecting the
/// tab the shell already shows still raises a change (the shell ignores null). Resolves the view
/// model lazily, so a build without a main window (gallery, tests) only answers false.
/// </summary>
public sealed class TabNavigator
{
    private readonly IServiceProvider _services;

    public TabNavigator(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <returns>False when there is no main window view model to drive.</returns>
    public bool Show(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_services.GetService<MainWindowViewModel>() is not { } window)
        {
            return false;
        }

        window.InitialTabKey = null;
        window.InitialTabKey = key;
        return true;
    }
}
