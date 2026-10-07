using Augram.App.Navigation;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Commands tab's <see cref="ICommandLocator"/>: a Global command is selected by the Global sub-tab's
/// view model (its category, or Uncategorized, expanded) and the shell goes to <c>commands.global</c>;
/// any other by the Apps sub-tab's (its group expanded) and the shell goes to <c>commands.apps</c>,
/// through <see cref="TabNavigator"/>.
/// </summary>
public sealed class CommandLocator : ICommandLocator
{
    private readonly CommandsViewModel _global;
    private readonly CommandsViewModel _apps;
    private readonly TabNavigator _tabs;

    public CommandLocator(CommandsViewModel global, CommandsViewModel apps, TabNavigator tabs)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(tabs);
        if (global.Scope != CommandsScope.Global)
        {
            throw new ArgumentException("Expected the Global tab's view model.", nameof(global));
        }

        if (apps.Scope != CommandsScope.Apps)
        {
            throw new ArgumentException("Expected the Apps tab's view model.", nameof(apps));
        }

        _global = global;
        _apps = apps;
        _tabs = tabs;
    }

    public bool ShowCommand(CommandId id)
    {
        if (_global.ShowCommand(id))
        {
            _tabs.Show(AppNavigation.CommandsGlobalKey);
            return true;
        }

        if (_apps.ShowCommand(id))
        {
            _tabs.Show(AppNavigation.CommandsAppsKey);
            return true;
        }

        return false;
    }
}
