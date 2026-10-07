using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GesturePicker;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandsViewModelTests
{
    [AvaloniaFact]
    public void ProjectsGlobalFirstThenGroupsAndCommandsByNameWithSummariesAndMarkers()
    {
        var (vm, _, _, _) = CommandsTestData.Create();

        Assert.Equal(["Global", "Apple", "Chrome"], vm.Groups.Select(group => group.Name));
        Assert.True(vm.Groups[0].IsGlobal);
        Assert.All(vm.Groups, group => Assert.False(group.IsExpanded));

        var global = vm.Groups[0].Commands;
        Assert.Equal(["Close window", "Three steps", "Volume up"], global.Select(command => command.Name));
        Assert.Equal(["Close window", "3 steps", "Volume up"], global.Select(command => command.StepSummary));
        Assert.Equal([TriggerKind.Gesture, TriggerKind.None, TriggerKind.WheelUp], global.Select(command => command.TriggerKind));
        Assert.Equal(["Up", "No trigger", "Wheel up"], global.Select(command => command.TriggerText));
        Assert.True(global[0].HasGlyph);
        Assert.False(global[2].HasGlyph);
        Assert.All(global, command => Assert.Null(command.PlatformMarker));

        var chrome = vm.Groups[2].Commands;
        Assert.Equal("Does nothing here", chrome.Single(command => command.Name == "Nothing on Up").StepSummary);
        Assert.Equal("has macOS override", chrome.Single(command => command.Name == "Close tab").PlatformMarker);
        Assert.False(vm.CanUndo);
        Assert.Null(vm.SelectedCommand);
    }

    [AvaloniaFact]
    public void SelectShowsTheCommandsStepsAndNewCommandLandsInTheSelectedGroupReadyToRename()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var renames = new List<CommandId>();
        vm.RenameRequested += (_, id) => renames.Add(id);
        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        var closeTab = chrome.Commands.Single(command => command.Name == "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, chrome, closeTab));

        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.Equal(chrome.Id, vm.SelectedGroupId);
        Assert.Equal("Wait 5 ms", Assert.Single(vm.Steps).Summary);
        Assert.Equal("has macOS override", vm.Steps[0].PlatformMarker);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        var created = CommandsTestData.Find(store, "New command 1");
        Assert.Contains(created, CommandsTestData.Group(store, "Chrome").Commands);
        Assert.Equal(Trigger.None, created.Trigger);
        Assert.Empty(created.Steps);
        Assert.Equal(created.Id, vm.SelectedCommandId);
        Assert.Equal([created.Id], renames);
        Assert.Empty(vm.Steps);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Contains(CommandsTestData.Group(store, "Global").Commands, command => command.Name == "New command 1");
    }

    [AvaloniaFact]
    public void RenameGoesThroughTheStoreAndRejectsADuplicateWithTheRuleMessage()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var global = vm.Groups[0];
        var close = global.Commands.Single(command => command.Name == "Close window");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, global, close, "volume up"));

        // The rule names whichever copy it met first; both are "volume up" when compared.
        Assert.Matches("^A command named '[Vv]olume up' already exists in 'Global'\\.$", vm.Message);
        Assert.Equal("Close window", store.FindCommand(close.Id)!.Value.Command.Name);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, global, close, "Close"));
        Assert.Null(vm.Message);
        Assert.Equal("Close", store.FindCommand(close.Id)!.Value.Command.Name);

        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, chrome, null, "Chromium"));
        Assert.Equal("Chromium", store.FindGroup(chrome.Id)!.Name);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, vm.Groups[0], null, "Everywhere"));
        Assert.Equal("The Global group cannot be renamed.", vm.Message);
        Assert.Equal("Global", store.Global.Name);
    }

    [AvaloniaFact]
    public void DeleteAsksFirstForCommandsAndGroupsAndUndoBringsThemBack()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var (vm, store, _, dialogs) = CommandsTestData.Create(confirm);
        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        var closeTab = chrome.Commands.Single(command => command.Name == "Close tab");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, chrome, closeTab));
        Assert.Equal(("Delete command", "Delete command 'Close tab'?", "Delete"), confirm.Requests[^1]);
        Assert.NotNull(store.FindCommand(closeTab.Id));

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, chrome, closeTab));
        Assert.Null(store.FindCommand(closeTab.Id));
        Assert.Equal($"Deleted 'Close tab'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, vm.Groups.Single(group => group.Name == "Chrome")));
        Assert.Equal(("Delete app group", "Delete group 'Chrome' and its command?", "Delete"), confirm.Requests[^1]);
        Assert.Empty(dialogs.Requests);
        Assert.Null(store.FindGroup(chrome.Id));
        Assert.Equal(["Global", "Apple"], vm.Groups.Select(group => group.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.NotNull(store.FindCommand(closeTab.Id));
        Assert.True(vm.CanRedo);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, vm.Groups[0]));
        Assert.Equal("The Global group cannot be deleted.", vm.Message);
        Assert.Equal(3, vm.Groups.Count);
    }

    [AvaloniaFact]
    public void NewGroupAndEditGroupGoThroughTheDeclaredFormAndKeepTheCommands()
    {
        var (vm, store, _, dialogs) = CommandsTestData.Create();
        dialogs.Answer = request =>
        {
            Field("Name", request).Set("Zed");
            Field("Executable names", request).Set("zed.exe, zed-preview.exe");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewGroup));

        Assert.Equal("New app group", dialogs.Last.Title);
        var zed = CommandsTestData.Group(store, "Zed");
        Assert.Equal(["zed.exe", "zed-preview.exe"], zed.Matcher!.ProcessNames);
        Assert.Equal(zed.Id, vm.SelectedGroupId);
        Assert.Equal(["Global", "Apple", "Chrome", "Zed"], vm.Groups.Select(group => group.Name));

        dialogs.Answer = request =>
        {
            Field("Name", request).Set("Chromium");
            Field("Window title", request).Set("^.*Chromium$");
            Toggle("Title is a regular expression", request).Set(true);
            return true;
        };
        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditGroup, chrome));

        Assert.Equal("Edit app group 'Chrome'", dialogs.Last.Title);
        var edited = store.FindGroup(chrome.Id)!;
        Assert.Equal("Chromium", edited.Name);
        Assert.Equal(["chrome.exe"], edited.Matcher!.ProcessNames);
        Assert.True(edited.Matcher.TitleIsRegex);
        Assert.Equal(2, edited.Commands.Count);

        dialogs.Answer = request =>
        {
            Field("Name", request).Set("Apple");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewGroup));
        Assert.Equal("An app group named 'Apple' already exists.", vm.Message);
    }

    [AvaloniaFact]
    public void ToggleActiveFlipsACommandOrAGroup()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var global = vm.Groups[0];
        var close = global.Commands[0];

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, global, close));
        Assert.False(store.FindCommand(close.Id)!.Value.Command.IsActive);
        Assert.False(vm.Groups[0].Commands[0].IsActive);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, vm.Groups.Single(group => group.Name == "Apple")));
        Assert.False(CommandsTestData.Group(store, "Apple").IsActive);
    }

    [AvaloniaFact]
    public void CopyPastesIntoAnotherGroupAndDropsATriggerThatGroupAlreadyUses()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var global = vm.Groups[0];
        var close = global.Commands.Single(command => command.Name == "Close window");
        var apple = vm.Groups.Single(group => group.Name == "Apple");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, global, close));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, apple));

        var pasted = Assert.Single(CommandsTestData.Group(store, "Apple").Commands);
        Assert.Equal("Close window", pasted.Name);
        Assert.NotEqual(close.Id, pasted.Id);
        Assert.Equal(Trigger.ForGesture(CommandsTestData.Up), pasted.Trigger);
        Assert.Equal("Close window", Assert.Single(pasted.Steps).Step.Summary);
        Assert.Equal(pasted.Id, vm.SelectedCommandId);

        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, chrome));

        var unbound = CommandsTestData.Group(store, "Chrome").Commands.Single(command => command.Name == "Close window");
        Assert.Equal(Trigger.None, unbound.Trigger);
        Assert.StartsWith("Pasted 'Close window' into 'Chrome' without its trigger", vm.Message, StringComparison.Ordinal);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, global));
        Assert.Contains(CommandsTestData.Group(store, "Global").Commands, command => command.Name == "Close window copy");
    }

    [AvaloniaFact]
    public void TriggerKindsSetWheelOrNoneAndGestureGoesThroughThePicker()
    {
        var (vm, store, picker, _) = CommandsTestData.Create();
        var global = vm.Groups[0];
        var close = global.Commands.Single(command => command.Name == "Close window");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, global, close, kind: TriggerKind.WheelDown));
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, global, close, kind: TriggerKind.WheelUp));
        Assert.Matches("^'(Volume up|Close window)' in 'Global' already uses wheel up\\.$", vm.Message);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, global, close, kind: TriggerKind.None));
        Assert.Equal(Trigger.None, store.FindCommand(close.Id)!.Value.Command.Trigger);

        picker.Result = GesturePickerResult.Selected(CommandsTestData.Down);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, global, close, kind: TriggerKind.Gesture));
        Assert.Equal([null], picker.Requests);
        Assert.Equal(Trigger.ForGesture(CommandsTestData.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);
        Assert.Equal("Down", vm.Groups[0].Commands.Single(command => command.Id == close.Id).TriggerText);

        picker.Result = GesturePickerResult.Cancelled;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.PickGesture, global, close));
        Assert.Equal(CommandsTestData.Down, picker.Requests[^1]);
        Assert.Equal(Trigger.ForGesture(CommandsTestData.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);

        picker.Result = GesturePickerResult.NoGesture;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.PickGesture, global, close));
        Assert.Equal(Trigger.None, store.FindCommand(close.Id)!.Value.Command.Trigger);
    }

    [AvaloniaFact]
    public void GroupsStartCollapsedStayAsTheUserLeftThemAndShowCommandExpandsOne()
    {
        var (vm, store, _, _) = CommandsTestData.Create();
        var chrome = vm.Groups.Single(group => group.Name == "Chrome");
        Assert.False(chrome.IsExpanded);
        Assert.Equal(2, chrome.Commands.Count);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, chrome));
        Assert.True(vm.Groups.Single(group => group.Name == "Chrome").IsExpanded);

        // A store change re-projects every group; what the user opened stays open.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, vm.Groups.Single(group => group.Name == "Apple")));
        Assert.True(vm.Groups.Single(group => group.Name == "Chrome").IsExpanded);
        Assert.False(vm.Groups.Single(group => group.Name == "Apple").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, vm.Groups.Single(group => group.Name == "Chrome")));
        Assert.False(vm.Groups.Single(group => group.Name == "Chrome").IsExpanded);

        var closeTab = CommandsTestData.Find(store, "Close tab");
        Assert.True(vm.ShowCommand(closeTab.Id));

        Assert.True(vm.Groups.Single(group => group.Name == "Chrome").IsExpanded);
        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.Equal("Close tab", vm.SelectedCommand!.Name);
        Assert.False(vm.ShowCommand(CommandId.New()));
    }

    private static IValueBinding<string> Field(string label, FormDialogRequest request)
        => (IValueBinding<string>)request.Screen!.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;

    private static IValueBinding<bool> Toggle(string label, FormDialogRequest request)
        => (IValueBinding<bool>)request.Screen!.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;
}
