using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.ViewModels.Ignored;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// The Ignored tab (F5 ignore list, F7; plan 0001 M2 step 6): one <see cref="MasterDetail"/> filling the tab, the ignored
/// apps beside the selected one's form (name, active, mode, and the app identification app groups use), bound to the
/// <see cref="IgnoredViewModel"/>. The component raises intents; the view model turns them into mapping store calls and opens
/// the new-app form and the delete confirmation through its presenters.
/// </summary>
public static class IgnoredScreen
{
    public static ScreenDeclaration Declare(IgnoredViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen("Ignored", () => Build(vm));
    }

    private static MasterDetail Build(IgnoredViewModel vm)
    {
        var view = new MasterDetail
        {
            DataContext = vm,
            Heading = vm.Heading,
            NewLabel = vm.NewLabel,
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
