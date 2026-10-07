using Augram.App.Components.Shell;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The "Used by…" jump: the right sub-tab of a real <see cref="Shell"/>, bound as <c>MainWindow</c> binds it, and the right section opened.</summary>
public sealed class CommandLocatorTests
{
    [AvaloniaFact]
    public void AGlobalCommandOpensTheGlobalSubTabAndItsCategoryAnAppCommandTheAppsSubTabAndItsGroup()
    {
        var (shell, services, store) = ShowShell();
        var locator = services.GetRequiredService<ICommandLocator>();
        var global = services.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Global);
        var apps = services.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Apps);
        Assert.Equal(("gestures", null), Selected(shell));

        var volume = Find(store, "Volume up");
        Assert.True(locator.ShowCommand(volume.Id));

        Assert.Equal((AppNavigation.CommandsKey, AppNavigation.CommandsGlobalKey), Selected(shell));
        Assert.Equal(volume.Id, global.SelectedCommandId);
        Assert.True(Section(global, "Media").IsExpanded);
        Assert.False(Section(global, "Window").IsExpanded);

        var closeTab = Find(store, "Close tab");
        Assert.True(locator.ShowCommand(closeTab.Id));

        Assert.Equal((AppNavigation.CommandsKey, AppNavigation.CommandsAppsKey), Selected(shell));
        Assert.Equal(closeTab.Id, apps.SelectedCommandId);
        Assert.True(Section(apps, "Chrome").IsExpanded);

        // Back to Global while the shell already shows the Commands tab: the sub-tab still switches.
        var three = Find(store, "Three steps");
        Assert.True(locator.ShowCommand(three.Id));
        Assert.Equal((AppNavigation.CommandsKey, AppNavigation.CommandsGlobalKey), Selected(shell));
        Assert.True(Section(global, "Uncategorized").IsExpanded);

        Assert.False(locator.ShowCommand(CommandId.New()));
        Assert.Equal((AppNavigation.CommandsKey, AppNavigation.CommandsGlobalKey), Selected(shell));
    }

    [AvaloniaFact]
    public void TheLocatorWantsEachTabsViewModelInItsPlace()
    {
        var (global, apps, _) = CreateBoth();
        var tabs = new TabNavigator(new ServiceCollection().BuildServiceProvider());

        Assert.Throws<ArgumentException>(() => new CommandLocator(apps, apps, tabs));
        Assert.Throws<ArgumentException>(() => new CommandLocator(global, global, tabs));
        Assert.NotNull(new CommandLocator(global, apps, tabs));
    }

    [Fact]
    public void TabNavigatorAnswersFalseWithoutAMainWindow()
    {
        var navigator = new TabNavigator(new ServiceCollection().BuildServiceProvider());

        Assert.False(navigator.Show(AppNavigation.CommandsGlobalKey));
    }

    /// <summary>A shell with a Gestures stand-in and the real Commands entry, its selected key bound to the main window view model.</summary>
    private static (Shell Shell, ServiceProvider Services, MappingStore Store) ShowShell()
    {
        var store = Store();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        CommandsModule.Register(services);
        services.AddSingleton(sp => new MainWindowViewModel(new NavigationRegistry(
        [
            new NavEntry("Gestures", AppNavigation.GesturesKey, () => new TextScreen("Gestures", "stand-in")),
            CommandsModule.NavEntry(sp),
        ]))
        { InitialTabKey = AppNavigation.GesturesKey });
        var provider = services.BuildServiceProvider();
        var main = provider.GetRequiredService<MainWindowViewModel>();
        var shell = new Shell { Registry = main.Registry };
        shell.Bind(Shell.SelectedKeyProperty, new Binding(nameof(MainWindowViewModel.InitialTabKey)) { Source = main });
        var window = new Window { Content = shell, Width = 900, Height = 600 };
        window.Show();
        return (shell, provider, store);
    }

    /// <summary>The selected top tab's key and, when it has sub-tabs, the selected sub-tab's key.</summary>
    private static (string? Tab, string? SubTab) Selected(Shell shell)
    {
        var tab = (TabItem)shell.GetVisualDescendants().OfType<TabControl>().First().SelectedItem!;
        var sub = (tab.Content as TabControl)?.SelectedItem as TabItem;
        return ((string?)tab.Tag, (string?)sub?.Tag);
    }
}
