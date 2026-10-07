using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The Apps tab's view model: one section per app group, Global left out, a category tag on the rows of a group that has categories.</summary>
public sealed class CommandsViewModelTests
{
    [AvaloniaFact]
    public void AppsTabShowsTheAppGroupsByNameWithoutGlobalWithSummariesMarkersAndCategoryTags()
    {
        var (vm, _, _, _) = Create();

        Assert.Equal(CommandsScope.Apps, vm.Scope);
        Assert.Equal(("App groups", "New group…"), (vm.Heading, vm.NewSectionLabel));
        Assert.Equal(["Apple", "Chrome", "Photoshop"], Names(vm));
        Assert.All(vm.Sections, section =>
        {
            Assert.False(section.IsExpanded);
            Assert.True(section is { CanRename: true, CanDelete: true, CanEditDefinition: true, CanToggleActive: true });
        });
        Assert.Equal("no commands", Section(vm, "Apple").CountText);

        var chrome = Section(vm, "Chrome").Commands;
        Assert.Equal(["Close tab", "Nothing on Up"], chrome.Select(command => command.Name));
        Assert.Equal(["Wait 5 ms", "Does nothing here"], chrome.Select(command => command.StepSummary));
        Assert.Equal("has macOS override", chrome[0].PlatformMarker);
        Assert.All(chrome, command =>
        {
            Assert.Null(command.CategoryLabel);
            Assert.Empty(command.Categories);
            Assert.Equal(Section(vm, "Chrome").Id, command.Section);
        });

        var photoshop = Section(vm, "Photoshop").Commands;
        Assert.Equal(["Brush", "Plain"], photoshop.Select(command => command.Name));
        Assert.Equal(["General", null], photoshop.Select(command => command.CategoryLabel));
        Assert.Equal(["Uncategorized", "Blend Mode Normal", "General"], photoshop[0].Categories.Select(choice => choice.Name));
        Assert.Null(photoshop[0].Categories[0].Id);
        Assert.False(vm.CanUndo);
        Assert.Null(vm.SelectedCommand);
    }

    [AvaloniaFact]
    public void SelectShowsTheCommandsStepsAndNewCommandLandsInTheSelectedGroupReadyToRename()
    {
        var (vm, store, _, _) = Create();
        var renames = new List<CommandId>();
        vm.RenameRequested += (_, id) => renames.Add(id);
        var chrome = Section(vm, "Chrome");
        var closeTab = Item(vm, "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, chrome, closeTab));

        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.Equal(chrome.Id, vm.SelectedSectionId);
        Assert.Equal("Wait 5 ms", Assert.Single(vm.Steps).Summary);
        Assert.Equal("has macOS override", vm.Steps[0].PlatformMarker);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        var created = Find(store, "New command 1");
        Assert.Contains(created, Group(store, "Chrome").Commands);
        Assert.Equal(Trigger.None, created.Trigger);
        Assert.Empty(created.Steps);
        Assert.Equal(created.Id, vm.SelectedCommandId);
        Assert.True(Section(vm, "Chrome").IsExpanded);
        Assert.Equal([created.Id], renames);
        Assert.Empty(vm.Steps);

        // The Apps tab has no group to fall back to.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Equal("Select an app group first, or make one with New group…", vm.Message);
        Assert.Equal(3, Group(store, "Chrome").Commands.Count);
        Assert.Single(renames);
    }

    [AvaloniaFact]
    public void RenameGoesThroughTheStoreAndRejectsADuplicateWithTheRuleMessage()
    {
        var (vm, store, _, _) = Create();
        var chrome = Section(vm, "Chrome");
        var closeTab = Item(vm, "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, chrome, closeTab, "nothing on up"));

        // The rule names whichever copy it met first; both are "nothing on up" when compared.
        Assert.Matches("^A command named '[Nn]othing on [Uu]p' already exists in 'Chrome'\\.$", vm.Message);
        Assert.Equal("Close tab", store.FindCommand(closeTab.Id)!.Value.Command.Name);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, chrome, closeTab, "Close"));
        Assert.Null(vm.Message);
        Assert.Equal("Close", store.FindCommand(closeTab.Id)!.Value.Command.Name);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, Section(vm, "Chrome"), null, "Chromium"));
        Assert.Equal("Chromium", store.FindGroup(chrome.Id.GroupId)!.Name);
        Assert.Equal(["Apple", "Chromium", "Photoshop"], Names(vm));
    }

    [AvaloniaFact]
    public void DeleteAsksFirstForCommandsAndGroupsAndUndoBringsThemBack()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var (vm, store, _, dialogs) = Create(confirm: confirm);
        var chrome = Section(vm, "Chrome");
        var closeTab = Item(vm, "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, chrome, closeTab));
        Assert.Equal(("Delete command", "Delete command 'Close tab'?", "Delete"), confirm.Requests[^1]);
        Assert.NotNull(store.FindCommand(closeTab.Id));

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, chrome, closeTab));
        Assert.Null(store.FindCommand(closeTab.Id));
        Assert.Equal($"Deleted 'Close tab'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Chrome")));
        Assert.Equal(("Delete app group", "Delete group 'Chrome' and its command?", "Delete"), confirm.Requests[^1]);
        Assert.Empty(dialogs.Requests);
        Assert.Null(store.FindGroup(chrome.Id.GroupId));
        Assert.Equal(["Apple", "Photoshop"], Names(vm));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.NotNull(store.FindCommand(closeTab.Id));
        Assert.True(vm.CanRedo);
        Assert.Equal(["Apple", "Chrome", "Photoshop"], Names(vm));
    }

    [AvaloniaFact]
    public void NewGroupGoesThroughTheDeclaredFormAndEditingIsTheSidePanel()
    {
        var (vm, store, _, dialogs) = Create();
        dialogs.Answer = request =>
        {
            Field("Name", request).Set("Zed");
            Field("Executable names", request).Set("zed.exe, zed-preview.exe");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewSection));

        Assert.Equal("New app group", dialogs.Last.Title);
        var zed = Group(store, "Zed");
        Assert.Equal(["zed.exe", "zed-preview.exe"], zed.Matcher!.ProcessNames);
        Assert.Equal(SectionId.ForGroup(zed.Id), vm.SelectedSectionId);
        Assert.Equal(["Apple", "Chrome", "Photoshop", "Zed"], Names(vm));

        var chrome = Section(vm, "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, chrome));
        var form = vm.GroupForm!;
        Field("Name", form).Set("Chromium");
        Field("Window title", form).Set("^.*Chromium$");
        Toggle("Title is a regular expression", form).Set(true);

        Assert.Same(form, vm.GroupForm);
        var edited = store.FindGroup(chrome.Id.GroupId)!;
        Assert.Equal("Chromium", edited.Name);
        Assert.Equal(["chrome.exe"], edited.Matcher!.ProcessNames);
        Assert.True(edited.Matcher.TitleIsRegex);
        Assert.Equal(2, edited.Commands.Count);

        dialogs.Answer = request =>
        {
            Field("Name", request).Set("Apple");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewSection));
        Assert.Equal("An app group named 'Apple' already exists.", vm.Message);
    }

    [AvaloniaFact]
    public void ToggleActiveFlipsACommandOrAGroup()
    {
        var (vm, store, _, _) = Create();
        var closeTab = Item(vm, "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, Section(vm, "Chrome"), closeTab));
        Assert.False(store.FindCommand(closeTab.Id)!.Value.Command.IsActive);
        Assert.False(Item(vm, "Close tab").IsActive);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, Section(vm, "Apple")));
        Assert.False(Group(store, "Apple").IsActive);
        Assert.False(Section(vm, "Apple").IsActive);
    }

    [AvaloniaFact]
    public void TheSidePanelShowsTheSelectedGroupsFormOnlyWhileAGroupRowIsSelected()
    {
        var (vm, _, _, _) = Create();
        Assert.Null(vm.GroupForm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Chrome")));
        Assert.Equal("Chrome", Field("Name", vm.GroupForm!).Get());
        Assert.Equal("chrome.exe", Field("Executable names", vm.GroupForm!).Get());

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Chrome"), Item(vm, "Close tab")));
        Assert.Null(vm.GroupForm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Photoshop")));
        Assert.Equal("Photoshop", Field("Name", vm.GroupForm!).Get());
    }

    [AvaloniaFact]
    public void APanelEditIsOneUndoStepAndUndoShowsInTheSameForm()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Chrome")));
        var form = vm.GroupForm!;

        Field("Executable names", form).Set("Google Chrome, chrome.exe");
        Assert.Equal(["Google Chrome", "chrome.exe"], Group(store, "Chrome").Matcher!.ProcessNames);
        Assert.True(vm.CanUndo);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(["chrome.exe"], Group(store, "Chrome").Matcher!.ProcessNames);
        Assert.Same(form, vm.GroupForm);
        Assert.Equal("chrome.exe", Field("Executable names", form).Get());
    }

    [AvaloniaFact]
    public void ARefusedNameShowsTheRuleAndTheFieldKeepsWhatWasTyped()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Chrome")));
        var name = Field("Name", vm.GroupForm!);

        name.Set("Apple");

        Assert.Equal("An app group named 'Apple' already exists.", vm.Message);
        Assert.Equal("Apple", name.Get());
        Assert.Equal(["Apple", "Chrome", "Photoshop"], Names(vm));
        Assert.False(vm.CanUndo);

        name.Set("Chromium");
        Assert.Null(vm.Message);
        Assert.Equal("Chromium", store.FindGroup(Section(vm, "Chromium").Id.GroupId)!.Name);
    }

    [AvaloniaFact]
    public void ARenameInTheTreeShowsInTheOpenForm()
    {
        var (vm, _, _, _) = Create();
        var chrome = Section(vm, "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, chrome));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, chrome, name: "Browser"));

        Assert.Equal("Browser", Field("Name", vm.GroupForm!).Get());
    }

    private static IValueBinding<string> Field(string label, FormDialogRequest request) => Field(label, request.Screen!);

    private static IValueBinding<bool> Toggle(string label, FormDialogRequest request) => Toggle(label, request.Screen!);

    private static IValueBinding<string> Field(string label, FormScreen screen)
        => (IValueBinding<string>)screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;

    private static IValueBinding<bool> Toggle(string label, FormScreen screen)
        => (IValueBinding<bool>)screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;
}
