using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.ViewModels.Ignored;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// One Ignored sub-tab (F5 ignore list, F7; plan 0001 M2 step 6; Global / Per command, plan 0004): one
/// <see cref="MasterDetail"/> filling it, the sub-tab's entries beside the selected one's form (name, active, the mode on
/// Global or "Used by" on Per command, and the app identification app groups use), bound to the <see cref="IgnoredViewModel"/>.
/// The component raises intents; the view model turns them into mapping store calls and opens the new-app form and the
/// delete and move confirmations through its presenters.
/// </summary>
public static class IgnoredScreen
{
    public static ScreenDeclaration Declare(IgnoredViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen(vm.IsPerCommand ? "Ignored › Per command" : "Ignored › Global", () => Build(vm));
    }

    private static MasterDetail Build(IgnoredViewModel vm)
    {
        var view = new MasterDetail
        {
            DataContext = vm,
            Heading = vm.Heading,
            NewLabel = vm.NewLabel,
            MoveLabel = vm.MoveLabel,
            HelpText = vm.Help,
            EmptyDetailText = vm.EmptyDetailText,
        };
        view.Bind(MasterDetail.ItemsProperty, new Binding(nameof(IgnoredViewModel.Items)));
        view.Bind(MasterDetail.SelectedIdProperty, new Binding(nameof(IgnoredViewModel.SelectedId)));
        view.Bind(MasterDetail.DetailProperty, new Binding(nameof(IgnoredViewModel.Detail)));
        view.Bind(MasterDetail.MessageProperty, new Binding(nameof(IgnoredViewModel.Message)));
        view.ActionRequested += (_, e) => vm.Handle(e);
        return view;
    }
}
