#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.GestureGrid;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.StepList;
using Augram.App.Components.Steps;
using Augram.App.Components.StepTypePicker;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the Commands tab's components (M2), all over <see cref="CommandGalleryFakes"/> and the starter gestures.</summary>
public static class CommandGalleryPages
{
    private static readonly IReadOnlyList<Gesture> Starter = StarterGestures.All();

    /// <summary>The Global sub-tab over a throwaway store: Uncategorized, then the categories Media and Window; New category… names one in place.</summary>
    public static ScreenDeclaration GlobalWorkbenchPage() => WorkbenchPage(CommandsScope.Global);

    /// <summary>The Apps sub-tab over the same fake mapping: Chrome, Photoshop (category tags, the header's Category dropdown), Steam games.</summary>
    public static ScreenDeclaration AppsWorkbenchPage() => WorkbenchPage(CommandsScope.Apps);

    /// <summary>A whole sub-tab over a throwaway store: tree, header, step list, with dialogs that answer at once.</summary>
    private static ScreenDeclaration WorkbenchPage(CommandsScope scope)
    {
        var library = new GestureLibrary(Starter);
        var store = new MappingStore(CommandGalleryFakes.Mapping());
        var vm = new CommandsViewModel(scope, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
        return Screens.CommandsScreen.Declare(vm);
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

        fields.Add(FormField("Window, Set size", new WindowOpStep(WindowOperation.SetSize, new WindowSize(1280, 720))));
        fields.Add(FormField("Imported, with parameters", new ImportedStep("SendKeys", "Send Ctrl+W", new Dictionary<string, string> { ["Keys"] = "^w", ["Delay"] = "0" })));
        return new FormScreen("Step forms", [new Section("One form per step type (StepFormRegistry)", fields)]);
    }

    public static ScreenDeclaration StepTypePickerPage() =>
        new FormScreen("StepTypePicker",
        [
            new Section("Every built-in type by category; Other is never offered",
            [
                new CustomField("Picker", () => new StepTypePicker { Types = StepRegistry.BuiltIn.All, Width = 260 }),
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
