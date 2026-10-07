using Augram.App.Components.CommandTree;
using Augram.App.Navigation;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandLocatorTests
{
    [AvaloniaFact]
    public void ShowCommandSelectsExpandsAndSwitchesTheShellToTheCommandsTab()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var window = new MainWindowViewModel(new NavigationRegistry([])) { InitialTabKey = AppNavigation.CommandsKey };
        var services = new ServiceCollection().AddSingleton(window).BuildServiceProvider();
        var locator = new CommandLocator(vm, new TabNavigator(services));
        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, chrome));
        var keys = new List<string?>();
        window.PropertyChanged += (_, _) => keys.Add(window.InitialTabKey);
        var closeTab = CommandsTestData.Find(store, "Close tab");

        Assert.True(locator.ShowCommand(closeTab.Id));

        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.True(vm.Groups.Single(group => group.Name == "Chrome").IsExpanded);
        Assert.Equal([null, AppNavigation.CommandsKey], keys);
        Assert.False(locator.ShowCommand(CommandId.New()));
    }

    [Fact]
    public void TabNavigatorAnswersFalseWithoutAMainWindow()
    {
        var navigator = new TabNavigator(new ServiceCollection().BuildServiceProvider());

        Assert.False(navigator.Show(AppNavigation.CommandsKey));
    }
}
