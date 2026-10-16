using Augram.App.Components.HeldInputList;
using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.ViewModels.Ignored;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// One Exclusions sub-tab (F5 ignore list, F7; plan 0001 M2 step 6; Global / Per command, plan 0004; "Exclusions" in the UI
/// since plan 0005; While holding, plan 0012): one <see cref="MasterDetail"/> filling it, the sub-tab's entries beside the
/// selected one's form (name, active, the mode and "Allowed for" on Global or "Used by" on Per command, and the app
/// identification app groups use), bound to the <see cref="IgnoredViewModel"/>. On While holding the form has the entry's
/// "Ignore gestures while holding" card under it (<see cref="HeldInputList"/>, the master-detail's footer).
/// The components raise intents; the view model turns them into mapping store calls and opens the new-app form and the
/// delete and move confirmations through its presenters.
/// </summary>
public static class IgnoredScreen
{
    public static ScreenDeclaration Declare(IgnoredViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen(vm.ScreenTitle, () => Build(vm));
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
            DetailFooter = vm.IsWhileHolding ? HeldInputs(vm) : null,
        };
        view.Bind(MasterDetail.ItemsProperty, new Binding(nameof(IgnoredViewModel.Items)));
        view.Bind(MasterDetail.SelectedIdProperty, new Binding(nameof(IgnoredViewModel.SelectedId)));
        view.Bind(MasterDetail.DetailProperty, new Binding(nameof(IgnoredViewModel.Detail)));
        view.Bind(MasterDetail.MessageProperty, new Binding(nameof(IgnoredViewModel.Message)));
        view.Bind(MasterDetail.SearchTextProperty, new Binding(nameof(IgnoredViewModel.SearchText)) { Mode = BindingMode.TwoWay });
        view.ActionRequested += (_, e) => vm.Handle(e);
        return view;
    }

    /// <summary>The While holding entry's card, bound to the view model's projection of it.</summary>
    private static HeldInputList HeldInputs(IgnoredViewModel vm)
    {
        var list = new HeldInputList { DataContext = vm, Platform = vm.Platform };
        list.Bind(HeldInputList.ItemsProperty, new Binding(nameof(IgnoredViewModel.HeldInputs)));
        list.Bind(HeldInputList.SelectedIndexProperty, new Binding(nameof(IgnoredViewModel.SelectedHeldInput)));
        list.Bind(HeldInputList.IsReadOnlyProperty, new Binding(nameof(IgnoredViewModel.HeldInputsReadOnly)));
        list.Bind(HeldInputList.SideProperty, new Binding(nameof(IgnoredViewModel.HeldInputSide)));
        list.Bind(HeldInputList.CopyToTextProperty, new Binding(nameof(IgnoredViewModel.HeldInputCopyToText)));
        list.Bind(Avalonia.Visual.IsVisibleProperty, new Binding(nameof(IgnoredViewModel.ShowsHeldInputs)));
        list.ActionRequested += (_, e) => vm.HandleHeldInput(e);
        return list;
    }
}
