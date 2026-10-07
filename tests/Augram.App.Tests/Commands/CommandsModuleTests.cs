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

namespace Augram.App.Tests.Commands;

/// <summary>The Commands slice of the composition root resolves on its own, keeps an earlier store, and its tab builds the workbench.</summary>
public sealed class CommandsModuleTests
{
    [AvaloniaFact]
    public void RegistersThePresentersTheViewModelAndTheLocatorAndKeepsAStoreRegisteredBefore()
    {
        var store = CommandsTestData.Store();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        CommandsModule.Register(services);
        using var provider = services.BuildServiceProvider();

        var vm = provider.GetRequiredService<CommandsViewModel>();
        Assert.Same(store, provider.GetRequiredService<MappingStore>());
        Assert.Equal(["Global", "Apple", "Chrome"], vm.Groups.Select(group => group.Name));
        Assert.NotEmpty(provider.GetRequiredService<GestureLibrary>().All);
        Assert.IsType<GesturePickerPresenter>(provider.GetRequiredService<IGesturePickerPresenter>());
        Assert.IsType<FormDialogPresenter>(provider.GetRequiredService<IFormDialogPresenter>());
        Assert.IsType<ConfirmPresenter>(provider.GetRequiredService<IConfirmPresenter>());
        Assert.IsType<CommandLocator>(provider.GetRequiredService<ICommandLocator>());

        var entry = CommandsModule.NavEntry(provider);
        Assert.Equal(AppNavigation.CommandsKey, entry.Key);
        var screen = Assert.IsType<ComponentScreen>(entry.Screen!());
        var bench = Assert.IsType<CommandsWorkbench>(screen.Build());
        var window = new Window { Content = bench, Width = 900, Height = 600 };
        window.Show();

        Assert.NotNull(bench.TreePart);
        Assert.NotNull(bench.HeaderPart);
        Assert.NotNull(bench.StepsPart);
        Assert.Equal(3, bench.TreePart.Rows.Count(row => row is GroupRow));
        Assert.False(bench.HasCommand);

        vm.ShowCommand(CommandsTestData.Find(store, "Three steps").Id);

        Assert.True(bench.HasCommand);
        Assert.Equal("Three steps", bench.TreePart.SelectedCommand!.Name);
        Assert.Equal(3, bench.StepsPart.Rows.Count);
    }
}
