using Augram.App.Components.CommandTree;
using Augram.App.Components.GestureGrid;
using Augram.App.Declarations;
using Augram.App.Screens;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Transfer;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// Where Export… lives (plan 0003, Question 4 as proposed) and what each entry point preselects: Options › Sync › Export and
/// import (everything), an app group's menu in Commands › Apps (that group), a category's or Uncategorized's menu in Commands ›
/// Global (Global), the Gestures toolbar (gestures only). Each shows the presenter's outcome on its own line.
/// </summary>
public sealed class ExportEntryPointsTests
{
    [Fact]
    public void AnAppGroupsMenuExportsThatGroup_AndAHoldRemapOrACommandOffersNoExport()
    {
        var export = new FakeExportPresenter { Outcome = "Exported Chrome." };
        var vm = Commands(CommandsScope.Apps, export, out var store);
        var chrome = CommandsTestData.Section(vm, "Chrome");

        Assert.True(CommandTreeMenu.Allows(CommandTreeAction.Export, chrome, null));
        Assert.False(CommandTreeMenu.Allows(CommandTreeAction.Export, chrome, chrome.Commands[0]));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Export, chrome));

        var start = Assert.IsType<ExportScope.Selection>(Assert.Single(export.Starts));
        Assert.Equal([CommandsTestData.Group(store, "Chrome").Id], start.Groups);
        Assert.Equal("Exported Chrome.", vm.Message);
    }

    [Fact]
    public void ACategoryOrUncategorizedExportsGlobal()
    {
        var export = new FakeExportPresenter();
        var vm = Commands(CommandsScope.Global, export, out _);

        Assert.All(vm.Sections, section => Assert.True(section.CanExport));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Export, CommandsTestData.Section(vm, "Window")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Export, CommandsTestData.Section(vm, "Uncategorized")));

        Assert.All(export.Starts, start => Assert.Equal([GroupId.Global], Assert.IsType<ExportScope.Selection>(start).Groups));
        Assert.Equal(2, export.Starts.Count);
    }

    [AvaloniaFact]
    public void TheMenuShowsExportOnASectionHeaderOnly()
    {
        var vm = Commands(CommandsScope.Apps, new FakeExportPresenter(), out _);
        var chrome = CommandsTestData.Section(vm, "Chrome");
        var menu = CommandTreeMenu.Build(_ => { });
        var export = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, CommandTreeAction.Export));

        CommandTreeMenu.Refresh(menu, chrome, null, vm.NewSectionLabel);
        Assert.Equal("Export…", export.Header);
        Assert.True(export.IsVisible);

        CommandTreeMenu.Refresh(menu, chrome, chrome.Commands[0], vm.NewSectionLabel);
        Assert.False(export.IsVisible);
    }

    [AvaloniaFact]
    public void TheGesturesToolbarExportsGesturesOnly()
    {
        var export = new FakeExportPresenter { Outcome = "Exported 12 gestures." };
        var vm = new GesturesViewModel(
            new GestureLibrary(StarterGestures.All()),
            () => RecognitionOptions.Default,
            new FakeTrainingPresenter(),
            new FakeImportPresenter(),
            new MappingStore(),
            new FakeUsedByPresenter(),
            new FakeConfirmPresenter(),
            export);

        vm.Handle(new GestureGridActionEventArgs(GestureGridAction.Export, null));

        Assert.Same(ExportScope.GesturesOnly, Assert.Single(export.Starts));
        Assert.Equal("Exported 12 gestures.", vm.Message);
        vm.Dispose();
    }

    [AvaloniaFact]
    public void OptionsSyncHasAnExportAndImportSection_ThatExportsEverything_AndImports()
    {
        var export = new FakeExportPresenter { Outcome = "Exported everything." };
        var import = new FakeAugramImportPresenter { Outcome = "Imported from Blender.augram.json: 1 app group added." };
        var strokesPlus = new FakeImportPresenter();
        var configuration = new ConfigurationViewModel(export, import, strokesPlus);
        var sync = TestAppBuilder.Services.GetRequiredService<SyncViewModel>();

        var screen = Assert.IsType<FormScreen>(OptionsScreen.DeclareSync(sync, configuration));

        // Options › Sync: the Sync section, then Export and import (plan 0006; Options › Configuration before About until then).
        Assert.Equal("Export and import", OptionsConfigurationSection.Title);
        Assert.Equal([OptionsSyncSection.Title, OptionsConfigurationSection.Title], screen.Sections.Select(section => section.Title));
        var section = screen.Sections.Single(candidate => candidate.Title == OptionsConfigurationSection.Title);
        Assert.Equal([OptionsConfigurationSection.AugramFileLabel, OptionsConfigurationSection.StrokesPlusLabel, "Last result"], section.Fields.Select(field => field.Label));
        Assert.False(section.Fields[2].Visible!.Get());

        // The fakes answer at once, so each flow has finished when it returns.
        Assert.True(configuration.ExportAsync().IsCompletedSuccessfully);
        Assert.True(configuration.ImportStrokesPlusAsync().IsCompletedSuccessfully);

        Assert.Same(ExportScope.Everything, Assert.Single(export.Starts));
        Assert.Equal(1, strokesPlus.Opened);
        Assert.Equal("Exported everything.", configuration.Status);
        Assert.True(section.Fields[2].Visible!.Get());

        Assert.True(configuration.ImportAsync().IsCompletedSuccessfully);
        Assert.Equal(1, import.Opened);
        Assert.Equal(import.Outcome, configuration.Status);
        import.Outcome = null;
        Assert.True(configuration.ImportAsync().IsCompletedSuccessfully);
        Assert.Equal("Imported from Blender.augram.json: 1 app group added.", configuration.Status);
        // A root without TransferModule leaves the section out; the sub-tab shows what it has.
        Assert.Equal([OptionsSyncSection.Title], Assert.IsType<FormScreen>(OptionsScreen.DeclareSync(sync, null)).Sections.Select(candidate => candidate.Title));
    }

    [AvaloniaFact]
    public void TheRunningAppRegistersExportForEveryEntryPoint()
    {
        var services = TestAppBuilder.Services;

        Assert.NotNull(services.GetService<IExportPresenter>());
        Assert.NotNull(services.GetService<IAugramImportPresenter>());
        Assert.NotNull(services.GetService<ConfigurationViewModel>());
    }

    private static CommandsViewModel Commands(CommandsScope scope, FakeExportPresenter export, out MappingStore store)
    {
        store = CommandsTestData.Store();
        return new CommandsViewModel(scope, store, new GestureLibrary(StarterGestures.All()), new FakeGesturePickerPresenter(), new FakeFormDialogPresenter(), new FakeConfirmPresenter(), new CommandClipboard(), HostPlatform.Windows, export);
    }
}
