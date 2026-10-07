using Augram.App.Navigation;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The Commands tab's <see cref="ICommandLocator"/>: selects the command in <see cref="CommandsViewModel"/> and switches the shell to the tab through <see cref="TabNavigator"/>.</summary>
public sealed class CommandLocator : ICommandLocator
{
    private readonly CommandsViewModel _commands;
    private readonly TabNavigator _tabs;

    public CommandLocator(CommandsViewModel commands, TabNavigator tabs)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(tabs);
        _commands = commands;
        _tabs = tabs;
    }

    public bool ShowCommand(CommandId id)
    {
        if (!_commands.ShowCommand(id))
        {
            return false;
        }

        _tabs.Show(AppNavigation.CommandsKey);
        return true;
    }
}
