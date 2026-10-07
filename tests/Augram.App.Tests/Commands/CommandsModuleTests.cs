using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.UsedBy;
using Augram.App.ViewModels.Commands;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The Commands slice of the composition root resolves on its own, keeps an earlier store, and its two sub-tabs build the workbench.</summary>
public sealed class CommandsModuleTests
{
    [AvaloniaFact]
    public void RegistersOneViewModelPerTabOverTheStoreRegisteredBeforeWithTheirPresentersAndTheLocator()
    {
        var store = Store();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        CommandsModule.Register(services);
        using var provider = services.BuildServiceProvider();

        var global = provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Global);
        var apps = provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Apps);
        Assert.Equal((CommandsScope.Global, CommandsScope.Apps), (global.Scope, apps.Scope));
        Assert.Same(global, provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Global));
        Assert.Same(store, provider.GetRequiredService<MappingStore>());
        Assert.Equal(["Uncategorized", "Media", "Window"], Names(global));
        Assert.Equal(["Apple", "Chrome", "Photoshop"], Names(apps));
        Assert.NotEmpty(provider.GetRequiredService<GestureLibrary>().All);
        Assert.IsType<GesturePickerPresenter>(provider.GetRequiredService<IGesturePickerPresenter>());
        Assert.IsType<FormDialogPresenter>(provider.GetRequiredService<IFormDialogPresenter>());
        Assert.IsType<ConfirmPresenter>(provider.GetRequiredService<IConfirmPresenter>());
        Assert.IsType<CommandLocator>(provider.GetRequiredService<ICommandLocator>());

        // One clipboard for both tabs.
        global.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(global, "Window"), Item(global, "Close window")));
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(apps, "Apple")));
        Assert.Equal("Close window", Assert.Single(Group(store, "Apple").Commands).Name);
    }

    [AvaloniaFact]
    public void TheTabHasAGlobalAndAnAppsSubTabEachAWorkbenchOverItsViewModel()
    {
        var store = Store();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        CommandsModule.Register(services);
        using var provider = services.BuildServiceProvider();

        var entry = CommandsModule.NavEntry(provider);
        Assert.Equal(AppNavigation.CommandsKey, entry.Key);
        Assert.Null(entry.Screen);
        Assert.Equal([("Global", AppNavigation.CommandsGlobalKey), ("Apps", AppNavigation.CommandsAppsKey)], entry.SubEntries!.Select(sub => (sub.Title, sub.Key)));

        var apps = Show(entry.SubEntries![1]);
        Assert.Equal(["Apple", "Chrome", "Photoshop"], apps.TreePart!.Rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Equal(("App groups", "New group…"), (apps.TreePart.Heading, apps.TreePart.NewSectionLabel));
        Assert.False(apps.HasCommand);

        provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Apps).ShowCommand(Find(store, "Close tab").Id);

        Assert.True(apps.HasCommand);
        Assert.Equal("Close tab", apps.TreePart.SelectedCommand!.Name);
        Assert.Single(apps.StepsPart!.Rows);

        var global = Show(entry.SubEntries[0]);
        Assert.Equal(["Uncategorized", "Media", "Window"], global.TreePart!.Rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Equal(("Global commands", "New category…"), (global.TreePart.Heading, global.TreePart.NewSectionLabel));
        Assert.False(global.HasCommand);
    }

    [Fact]
    public void WithoutRegisterEachSubTabSaysSoInsteadOfFailing()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var entry = CommandsModule.NavEntry(provider);

        Assert.All(entry.SubEntries!, sub => Assert.IsType<TextScreen>(sub.Screen!()));
    }

    private static CommandsWorkbench Show(NavEntry entry)
    {
        var screen = Assert.IsType<ComponentScreen>(entry.Screen!());
        var bench = Assert.IsType<CommandsWorkbench>(screen.Build());
        var window = new Window { Content = bench, Width = 900, Height = 600 };
        window.Show();
        Assert.NotNull(bench.TreePart);
        Assert.NotNull(bench.HeaderPart);
        Assert.NotNull(bench.StepsPart);
        return bench;
    }
}
