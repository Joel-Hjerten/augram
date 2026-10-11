#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GestureGrid;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.StepList;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.DisplayMode;
using Augram.App.Components.StepTypePicker;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.Scroll;
using Augram.Core.Steps.TypeText;
using Augram.Core.Steps.WindowOp;
using Avalonia.Data;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the Commands tab's components (M2), all over <see cref="CommandGalleryFakes"/> and the starter gestures.</summary>
public static class CommandGalleryPages
{
    private static readonly IReadOnlyList<Gesture> Starter = StarterGestures.All();

    /// <summary>
    /// The Global sub-tab over a throwaway store: Uncategorized, then the categories Media and Window; New category names one in
    /// place. Window is open and selected, so its header shows New command and the side panel its form.
    /// </summary>
    public static ScreenDeclaration GlobalWorkbenchPage()
    {
        var vm = WorkbenchViewModel(CommandsScope.Global, out _);
        var window = vm.Sections.Single(section => section.Name == "Window");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, window));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, window));
        return Screens.CommandsScreen.Declare(vm);
    }

    /// <summary>
    /// The Apps sub-tab over the same fake mapping: Chrome, Photoshop (category tags, the header's Category dropdown), Steam games.
    /// Chrome's Close tab is selected, so Chrome's header shows New command.
    /// </summary>
    public static ScreenDeclaration AppsWorkbenchPage()
    {
        var vm = WorkbenchViewModel(CommandsScope.Apps, out var store);
        vm.ShowCommand(store.Current.AllCommands().Single(pair => pair.Group.Name == "Chrome" && pair.Command.Name == "Close tab").Command.Id);
        return Screens.CommandsScreen.Declare(vm);
    }

    /// <summary>
    /// The Apps sub-tab with Blender's Space hold remap open and Orbit selected (F9, plan 0002): the hold remap nested in its
    /// group with its four commands, the header's Input (Left) and the Remap step's form (Middle).
    /// </summary>
    public static ScreenDeclaration HoldRemapsPage()
    {
        var vm = WorkbenchViewModel(CommandsScope.Apps, out var store);
        vm.ShowCommand(store.Current.AllCommands().Single(pair => pair.Command.Name == "Orbit").Command.Id);
        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[0]));
        return Screens.CommandsScreen.Declare(vm);
    }

    /// <summary>
    /// The header's Drag distance and Not in rows (plan 0004) over <see cref="CommandGalleryFakes.MappingWithZoom"/>, each header
    /// live over its own throwaway store: Global's Zoom in with its own 3 px, not used over Spine and Eyeris; Zoom out on the
    /// Options value (12 px here) and used everywhere; Chrome's Zoom in (an app command has both rows), not used over Eyeris;
    /// Volume up (the stroke button: no drag distance); then the Not in dialog for Zoom in (the Per command entries by name, two
    /// ticked, Krita inactive, and Add app… with its magnifier, which adds the window dropped on), and the same dialog over a
    /// mapping without Per command entries.
    /// </summary>
    public static ScreenDeclaration DragDistanceNotInPage()
    {
        var mapping = MappingRules.ValidDocument(CommandGalleryFakes.MappingWithZoom());
        var zoomIn = mapping.Global.Commands.Single(command => command.Name == "Zoom in");
        return new FormScreen("Drag distance and Not in",
        [
            new Section("Global › Zoom in: Right + wheel up with its own 3 px; not used over Spine and Eyeris",
            [
                new CustomField("Zoom in", () => LiveHeader(CommandsScope.Global, "Zoom in")),
            ]),
            new Section("Global › Zoom out: the Options value (12 px here); used everywhere",
            [
                new CustomField("Zoom out", () => LiveHeader(CommandsScope.Global, "Zoom out", optionsDragDistancePx: 12)),
            ]),
            new Section("Chrome › Zoom in: an app command has both rows; not used over Eyeris",
            [
                new CustomField("Chrome", () => LiveHeader(CommandsScope.Apps, "Zoom in")),
            ]),
            new Section("Global › Volume up: held with the stroke button, so no drag distance",
            [
                new CustomField("Volume up", () => LiveHeader(CommandsScope.Global, "Volume up")),
            ]),
            new Section("The Not in dialog for Zoom in: the Per command entries by name, two ticked; Add app… adds the window dropped on",
            [
                new CustomField("Not in", () => NotInDialog(NotInEditViewModel.For(zoomIn, mapping, HostPlatform.Windows))),
            ]),
            new Section("The Not in dialog with nothing on Per command yet",
            [
                new CustomField("Empty", () => NotInDialog(NotInEditViewModel.For(zoomIn, mapping with { Ignored = [] }, HostPlatform.Windows))),
            ]),
        ]);
    }

    private static FormDialog NotInDialog(NotInEditViewModel edit)
    {
        var dialog = FormDialogPresenter.Build(new FormDialogRequest(NotInEditViewModel.Title, NotInEditViewModel.ConfirmLabel, Screen: edit.Declare()));
        dialog.Width = 560;
        return dialog;
    }

    /// <summary>A command header bound to a view model over a throwaway store with <paramref name="name"/> selected; its edits go to that store.</summary>
    private static CommandHeader LiveHeader(CommandsScope scope, string name, int? optionsDragDistancePx = null)
    {
        var library = new GestureLibrary(Starter);
        var store = new MappingStore(CommandGalleryFakes.MappingWithZoom());
        var vm = new CommandsViewModel(scope, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
        if (optionsDragDistancePx is { } px)
        {
            vm.OptionsDragDistancePx = px;
        }

        var found = store.Current.AllCommands().First(pair => pair.Command.Name == name && CommandSections.Includes(scope, pair.Group));
        vm.ShowCommand(found.Command.Id);
        var header = new CommandHeader { Width = 760 };
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        return header;
    }

    /// <summary>A whole sub-tab's view model over a throwaway store (tree, header, step list), with dialogs that answer at once.</summary>
    private static CommandsViewModel WorkbenchViewModel(CommandsScope scope, out MappingStore store)
    {
        var library = new GestureLibrary(Starter);
        store = new MappingStore(CommandGalleryFakes.Mapping());
        return new CommandsViewModel(scope, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
    }

    public static ScreenDeclaration StepListPage()
    {
        var steps = FakeSteps().Select((step, index) => StepItem.From(step, index, Hosting.CommandsModule.CurrentPlatform)).ToList();
        return new FormScreen("StepList",
        [
            new Section("Four steps, the first expanded; drag to reorder, the check box toggles (local, no store)",
            [
                new CustomField("StepList", () => BuildLiveStepList(steps)),
            ]),
            new Section("No command selected",
            [
                new CustomField("Empty", () => new StepList { HasCommand = false, Width = 520 }),
            ]),
        ]);
    }

    /// <summary>Every built-in type's form, as the step list expands it; edits only print the new summary.</summary>
    public static ScreenDeclaration StepFormsPage()
    {
        var fields = new List<Field>();
        foreach (var type in StepRegistry.BuiltIn.All)
        {
            fields.Add(FormField(type.DisplayName, type.CreateDefault()));
        }

        fields.Add(new CustomField("Display mode, over a 4K TV", () => new DisplayModeStepForm(GalleryDisplayModes.Instance).Build(new DisplayModeStep(new DisplayResolution(3840, 2160), RefreshRate.FromHertz(119.88)), _ => { })));
        fields.Add(FormField("Window, Set size", new WindowOpStep(WindowOperation.SetSize, new WindowSize(1280, 720))));
        fields.Add(FormField("Type text, three lines by keys", new TypeTextStep("first line\nsecond line, long enough to wrap inside the field when the window is narrow\nthird", TypeTextMethod.Keys)));
        fields.Add(FormField("Run, as admin and hidden", new RunStep("taskkill.exe", "/f /im yuzu.exe", Elevated: true, Hidden: true)));
        fields.Add(FormField("Scroll, Ctrl held, three notches", new ScrollStep(ScrollDirection.Down, 3, KeyModifiers.Control)));
        fields.Add(FormField("Remap, a key with Ctrl+Shift+RAlt (Blender's Rotate)", new RemapStep(new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt, KeyModifiers.Alt))));
        fields.Add(FormField("Remap, a wheel notch with Ctrl", new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control))));
        fields.Add(FormField("Imported, with parameters", new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w", ["Delay"] = "0" })));
        return new FormScreen("Step forms", [new Section("One form per step type (StepFormRegistry)", fields)]);
    }

    public static ScreenDeclaration StepTypePickerPage() =>
        new FormScreen("StepTypePicker",
        [
            new Section("Every built-in type by category; Other is never offered, nor Remap outside a hold remap",
            [
                new CustomField("Picker", () => new StepTypePicker { Types = StepRegistry.BuiltIn.All, Width = 260 }),
            ]),
            new Section("Under a hold remap: Remap is offered too (Mouse)",
            [
                new CustomField("Picker", () => new StepTypePicker { Types = StepRegistry.BuiltIn.All, UnderHoldRemap = true, Width = 260 }),
            ]),
        ]);

    public static ScreenDeclaration GesturePickerPage() =>
        new FormScreen("GesturePicker",
        [
            new Section("The Select Gesture popup's content over the starter set, Circle preselected",
            [
                new CustomField("GesturePicker", () =>
                {
                    var picker = new GesturePicker { Tiles = [.. Starter.Select(GestureTileItem.From)], Width = 560, Height = 420 };
                    picker.Select(StarterGestures.IdFor("Circle"));
                    return picker;
                }),
            ]),
        ]);

    public static ScreenDeclaration FormDialogPage() =>
        new FormScreen("FormDialog",
        [
            new Section("The app group form: a declared screen",
            [
                new CustomField("New app group", () => new FormDialog { Screen = new GroupEditViewModel().Declare(), ConfirmLabel = "Create", Width = 560 }),
            ]),
            new Section("With a message line above the form",
            [
                new CustomField("Edit app group", () => new FormDialog { Message = "Chrome has 2 commands; they stay as they are.", Screen = GroupEditViewModel.From(CommandGalleryFakes.Mapping().Groups[1]).Declare(), ConfirmLabel = "Save", Width = 560 }),
            ]),
            new Section("The hold remap form (the Apps tab's side panel): name, hold key, tap time, active, Use on",
            [
                new CustomField("Hold remap", () => new FormDialog { Screen = HoldRemapEditViewModel.From(CommandGalleryFakes.Blender().HoldRemaps[0]).Declare(), ConfirmLabel = "Save", Width = 560 }),
            ]),
            new Section("The category form (the Global tab's side panel): name and Use on, here Windows only",
            [
                new CustomField("Category", () => new FormDialog { Screen = CategoryEditViewModel.From(CommandGalleryFakes.Mapping().Global.Categories.Single(category => category.Name == "PC tools")).Declare(), ConfirmLabel = "Save", Width = 560 }),
            ]),
        ]);

    private static CustomField FormField(string label, IStep step)
        => new(label, () => StepFormRegistry.Default.Build(step, _ => { }));

    private static StepList BuildLiveStepList(List<StepItem> steps)
    {
        var list = new StepList { Steps = steps, SelectedIndex = 0, StepTypes = StepRegistry.BuiltIn.All, HasCommand = true, Width = 520 };
        list.ActionRequested += (_, e) =>
        {
            var items = list.Steps.Select(item => item.Step).ToList();
            switch (e.Action)
            {
                case StepListAction.Select when e.Step is { } step:
                    list.SelectedIndex = step.Index;
                    return;
                case StepListAction.Reorder when e.Step is { } step:
                    var moved = items[step.Index];
                    items.RemoveAt(step.Index);
                    items.Insert(e.TargetIndex, moved);
                    list.SelectedIndex = e.TargetIndex;
                    break;
                case StepListAction.Delete when e.Step is { } step:
                    items.RemoveAt(step.Index);
                    break;
                case StepListAction.Add when e.Type is { } type:
                    items.Add(new CommandStep(type.CreateDefault(), HostPlatform.Windows));
                    list.SelectedIndex = items.Count - 1;
                    break;
                case StepListAction.Edit when e.Step is { } step && e.Edited is { } edited:
                    items[step.Index] = items[step.Index] with { Step = edited };
                    break;
                case StepListAction.ToggleActive when e.Step is { } step:
                    items[step.Index] = items[step.Index] with { IsActive = !items[step.Index].IsActive };
                    break;
                default:
                    return;
            }

            list.Steps = items.Select((step, index) => StepItem.From(step, index, Hosting.CommandsModule.CurrentPlatform)).ToList();
        };
        return list;
    }

    private static IReadOnlyList<CommandStep> FakeSteps() =>
    [
        new(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
        new(new DelayStep(50), HostPlatform.Windows),
        new(new MediaKeyStep(MediaKeyKind.PlayPause), HostPlatform.Windows, IsActive: false),
        new(new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w" }), HostPlatform.Windows),
    ];
}
#endif
