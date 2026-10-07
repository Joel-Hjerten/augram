using Augram.App.Components.CommandsWorkbench;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// The Commands tab (F5a, F7): one <see cref="CommandsWorkbench"/> filling the tab, bound to
/// <see cref="CommandsViewModel"/>. The workbench's parts raise intents; the view model turns them
/// into mapping store calls. The gesture picker and the group form are opened by the view model's presenters.
/// </summary>
public static class CommandsScreen
{
    public static ScreenDeclaration Declare(CommandsViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen("Commands", () => Build(vm));
    }

    private static CommandsWorkbench Build(CommandsViewModel vm)
    {
        var bench = new CommandsWorkbench { DataContext = vm, StepTypes = vm.StepTypes };
        bench.Bind(CommandsWorkbench.GroupsProperty, new Binding(nameof(CommandsViewModel.Groups)));
        bench.Bind(CommandsWorkbench.SelectedGroupIdProperty, new Binding(nameof(CommandsViewModel.SelectedGroupId)));
        bench.Bind(CommandsWorkbench.SelectedCommandIdProperty, new Binding(nameof(CommandsViewModel.SelectedCommandId)));
        bench.Bind(CommandsWorkbench.SelectedCommandProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)));
        bench.Bind(CommandsWorkbench.StepsProperty, new Binding(nameof(CommandsViewModel.Steps)));
        bench.Bind(CommandsWorkbench.SelectedStepIndexProperty, new Binding(nameof(CommandsViewModel.SelectedStepIndex)));
        bench.Bind(CommandsWorkbench.CanUndoProperty, new Binding(nameof(CommandsViewModel.CanUndo)));
        bench.Bind(CommandsWorkbench.CanRedoProperty, new Binding(nameof(CommandsViewModel.CanRedo)));
        bench.Bind(CommandsWorkbench.MessageProperty, new Binding(nameof(CommandsViewModel.Message)));
        bench.TreeActionRequested += (_, e) => vm.Handle(e);
        bench.StepActionRequested += (_, e) => vm.Handle(e);
        vm.RenameRequested += (_, id) => bench.BeginRename(id);
        return bench;
    }
}
