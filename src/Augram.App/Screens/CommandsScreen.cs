using Augram.App.Components.CommandsWorkbench;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// A Commands sub-tab (F5a, F7; Global and Apps since 2026-10-07): one <see cref="CommandsWorkbench"/>
/// filling the tab, bound to that tab's <see cref="CommandsViewModel"/>. Both sub-tabs are this screen;
/// the view model's <see cref="CommandsViewModel.Scope"/> decides the sections and the words. The
/// workbench's parts raise intents; the view model turns them into mapping store calls. The gesture
/// picker, the new-group form and the confirmations are opened by the view model's presenters; the selected
/// group's form shows in the side panel.
/// </summary>
public static class CommandsScreen
{
    public static ScreenDeclaration Declare(CommandsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen(vm.Heading, () => Build(vm));
    }

    private static CommandsWorkbench Build(CommandsViewModel vm)
    {
        var bench = new CommandsWorkbench
        {
            DataContext = vm,
            StepTypes = vm.StepTypes,
            TreeHeading = vm.Heading,
            NewSectionLabel = vm.NewSectionLabel,
            PlatformFilterLabel = vm.PlatformFilterLabel,
            TreeHelp = vm.Help,
        };
        bench.Bind(CommandsWorkbench.SectionsProperty, new Binding(nameof(CommandsViewModel.Sections)));
        bench.Bind(CommandsWorkbench.SelectedSectionIdProperty, new Binding(nameof(CommandsViewModel.SelectedSectionId)));
        bench.Bind(CommandsWorkbench.SelectedCommandIdProperty, new Binding(nameof(CommandsViewModel.SelectedCommandId)));
        bench.Bind(CommandsWorkbench.SelectedCommandProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)));
        bench.Bind(CommandsWorkbench.StepsProperty, new Binding(nameof(CommandsViewModel.Steps)));
        bench.Bind(CommandsWorkbench.GroupFormProperty, new Binding(nameof(CommandsViewModel.GroupForm)));
        bench.Bind(CommandsWorkbench.ShowsOtherPlatformsProperty, new Binding(nameof(CommandsViewModel.ShowOtherPlatforms)));
        bench.Bind(CommandsWorkbench.SelectedStepIndexProperty, new Binding(nameof(CommandsViewModel.SelectedStepIndex)));
        bench.Bind(CommandsWorkbench.MessageProperty, new Binding(nameof(CommandsViewModel.Message)));
        bench.TreeActionRequested += (_, e) => vm.Handle(e);
        bench.StepActionRequested += (_, e) => vm.Handle(e);
        vm.RenameRequested += (_, id) => bench.BeginRename(id);
        vm.SectionRenameRequested += (_, id) => bench.BeginRename(id);
        return bench;
    }
}
