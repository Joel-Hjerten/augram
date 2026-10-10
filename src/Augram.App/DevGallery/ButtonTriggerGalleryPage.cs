#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia.Data;

namespace Augram.App.DevGallery;

/// <summary>
/// The Button trigger (plan 0005, Joel 2026-10-10: "Right + Left", Eyeris's loupe chord in Augram) over
/// <see cref="CommandGalleryFakes.MappingWithMagnifier"/>, each part live over its own throwaway store: the Global workbench with
/// Magnifier selected (the pressed button beside the kind, the While holding boxes with the stroke button's and Left's locked, the
/// Drag distance row, the Also in row naming Blender, its Remap step's form offering a key only, the step picker offering
/// Remap); headers in the states of the trigger: Magnifier as stored (with its Also in row), pressed with Right instead (the
/// draft waits for a button to hold), and Back given Left, which Magnifier uses (Take it and Swap in the note); and the Also in
/// dialog (plan 0005 decision 7) with entries and with none.
/// </summary>
public static class ButtonTriggerGalleryPage
{
    /// <summary>The Global sub-tab with Magnifier selected and its Remap step open.</summary>
    public static ScreenDeclaration WorkbenchPage()
    {
        var (vm, store) = ViewModel();
        vm.ShowCommand(Find(store, "Magnifier").Id);
        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[0]));
        return Screens.CommandsScreen.Declare(vm);
    }

    public static ScreenDeclaration Page() => new FormScreen("Button trigger",
    [
        new Section("Global › Media › Magnifier: Right + Left, with Left's and the stroke button's boxes locked",
        [
            new CustomField("Magnifier", () => Header("Magnifier")),
        ]),
        new Section("Magnifier pressed with Right instead: the draft waits for a button to hold",
        [
            new CustomField("Right pressed", () => Header("Magnifier", item => new(CommandTreeAction.SetTriggerButton, command: item, button: MouseButton.Right))),
        ]),
        new Section("Back given Left, which Magnifier uses: Take it and Swap in the note",
        [
            new CustomField("Back", () => Header("Back", item => new(CommandTreeAction.SetTriggerButton, command: item, button: MouseButton.Left))),
        ]),
        new Section("The Also in dialog for Magnifier: the plain Exclusions › Global entries by name, Blender ticked, A Plague Tale inactive (VMware disables Augram while focused: not offered)",
        [
            new CustomField("Also in", () => AlsoInDialog(MappingRules.ValidDocument(CommandGalleryFakes.MappingWithMagnifier()))),
        ]),
        new Section("The Also in dialog with no plain entry on Exclusions › Global",
        [
            new CustomField("Empty", () =>
            {
                var mapping = MappingRules.ValidDocument(CommandGalleryFakes.MappingWithMagnifier());
                return AlsoInDialog(mapping with { Ignored = [.. mapping.Ignored.Where(app => !AlsoInEditViewModel.Offered(app))] });
            }),
        ]),
    ]);

    private static FormDialog AlsoInDialog(MappingDocument mapping)
    {
        var edit = AlsoInEditViewModel.For(mapping.Global.Commands.Single(command => command.Name == "Magnifier"), mapping);
        var dialog = FormDialogPresenter.Build(new FormDialogRequest(AlsoInEditViewModel.Title, AlsoInEditViewModel.ConfirmLabel, Screen: edit.Declare()));
        dialog.Width = 560;
        return dialog;
    }

    /// <summary>A command header bound to a view model over a throwaway store, <paramref name="name"/> selected and the edit <paramref name="draft"/> asks for applied.</summary>
    private static CommandHeader Header(string name, Func<CommandItem, CommandTreeActionEventArgs>? draft = null)
    {
        var (vm, store) = ViewModel();
        vm.ShowCommand(Find(store, name).Id);
        if (draft is not null)
        {
            vm.Handle(draft(vm.SelectedCommand!));
        }

        var header = new CommandHeader { Width = 760 };
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        return header;
    }

    private static (CommandsViewModel Vm, MappingStore Store) ViewModel()
    {
        var library = new GestureLibrary(StarterGestures.All());
        var store = new MappingStore(CommandGalleryFakes.MappingWithMagnifier());
        var vm = new CommandsViewModel(CommandsScope.Global, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
        return (vm, store);
    }

    private static Command Find(MappingStore store, string name) => store.Current.Global.Commands.Single(command => command.Name == name);
}
#endif
