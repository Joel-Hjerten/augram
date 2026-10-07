using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.App.ViewModels.Commands;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>What both Commands tabs share: the clipboard (copy on one, paste on the other), the command header's trigger, and the expanded memory, kept per tab.</summary>
public sealed class CommandsViewModelSharedTests
{
    [AvaloniaFact]
    public void ACommandCopiedOnTheGlobalTabPastesIntoAnAppGroupAndDropsATriggerThatGroupAlreadyUses()
    {
        var (global, apps, store) = CreateBoth();
        var close = Item(global, "Close window");

        global.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(global, "Window"), close));
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(apps, "Apple")));

        var pasted = Assert.Single(Group(store, "Apple").Commands);
        Assert.Equal("Close window", pasted.Name);
        Assert.NotEqual(close.Id, pasted.Id);
        Assert.Equal(Trigger.ForGesture(Up), pasted.Trigger);
        Assert.Null(pasted.CategoryId);
        Assert.Equal("Close window", Assert.Single(pasted.Steps).Step.Summary);
        Assert.Equal(pasted.Id, apps.SelectedCommandId);
        Assert.True(Section(apps, "Apple").IsExpanded);

        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(apps, "Chrome")));

        var unbound = Group(store, "Chrome").Commands.Single(command => command.Name == "Close window");
        Assert.Equal(Trigger.None, unbound.Trigger);
        Assert.StartsWith("Pasted 'Close window' into 'Chrome' without its trigger", apps.Message, StringComparison.Ordinal);

        // Within Photoshop the copy keeps its category.
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(apps, "Photoshop"), Item(apps, "Brush")));
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(apps, "Photoshop")));
        Assert.Equal(Find(store, "Brush").CategoryId, Find(store, "Brush copy").CategoryId);
        Assert.Equal("General", Item(apps, "Brush copy").CategoryLabel);
    }

    [AvaloniaFact]
    public void TriggerKindsSetWheelOrNoneAndGestureGoesThroughThePicker()
    {
        var (vm, store, picker, _) = Create(CommandsScope.Global);
        var window = Section(vm, "Window");
        var close = Item(vm, "Close window");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, window, close, kind: TriggerKind.WheelDown));
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, window, close, kind: TriggerKind.WheelUp));
        Assert.Matches("^'(Volume up|Close window)' in 'Global' already uses wheel up\\.$", vm.Message);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), store.FindCommand(close.Id)!.Value.Command.Trigger);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, window, close, kind: TriggerKind.None));
        Assert.Equal(Trigger.None, store.FindCommand(close.Id)!.Value.Command.Trigger);

        picker.Result = GesturePickerResult.Selected(Left);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerKind, window, close, kind: TriggerKind.Gesture));
        Assert.Equal([null], picker.Requests);
        Assert.Equal(Trigger.ForGesture(Left), store.FindCommand(close.Id)!.Value.Command.Trigger);
        Assert.Equal("Left", Item(vm, "Close window").TriggerText);

        picker.Result = GesturePickerResult.Cancelled;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.PickGesture, window, close));
        Assert.Equal(Left, picker.Requests[^1]);
        Assert.Equal(Trigger.ForGesture(Left), store.FindCommand(close.Id)!.Value.Command.Trigger);

        picker.Result = GesturePickerResult.NoGesture;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.PickGesture, window, close));
        Assert.Equal(Trigger.None, store.FindCommand(close.Id)!.Value.Command.Trigger);
    }

    [AvaloniaFact]
    public void SectionsStartCollapsedStayAsTheUserLeftThemPerTabAndShowCommandOpensTheRightOne()
    {
        var (global, apps, store) = CreateBoth();
        Assert.All(global.Sections.Concat(apps.Sections), section => Assert.False(section.IsExpanded));

        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(apps, "Chrome")));
        global.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(global, "Media")));
        Assert.True(Section(apps, "Chrome").IsExpanded);
        Assert.True(Section(global, "Media").IsExpanded);
        Assert.Equal([false, true, false], global.Sections.Select(section => section.IsExpanded));

        // A store change re-projects both tabs; what the user opened stays open on each.
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, Section(apps, "Apple")));
        Assert.True(Section(apps, "Chrome").IsExpanded);
        Assert.False(Section(apps, "Apple").IsExpanded);
        Assert.True(Section(global, "Media").IsExpanded);

        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(apps, "Chrome")));
        Assert.False(Section(apps, "Chrome").IsExpanded);

        var closeTab = Find(store, "Close tab");
        Assert.False(global.ShowCommand(closeTab.Id));
        Assert.True(apps.ShowCommand(closeTab.Id));
        Assert.True(Section(apps, "Chrome").IsExpanded);
        Assert.Equal(closeTab.Id, apps.SelectedCommandId);
        Assert.Equal("Close tab", apps.SelectedCommand!.Name);

        var three = Find(store, "Three steps");
        Assert.False(apps.ShowCommand(three.Id));
        Assert.True(global.ShowCommand(three.Id));
        Assert.True(Section(global, "Uncategorized").IsExpanded);
        Assert.Equal(SectionId.Uncategorized, global.SelectedSectionId);
        Assert.False(apps.ShowCommand(CommandId.New()));
    }
}
