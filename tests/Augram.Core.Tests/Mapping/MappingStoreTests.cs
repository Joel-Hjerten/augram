using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

public sealed class MappingStoreTests
{
    [Fact]
    public void AFreshStoreHoldsOnlyAnEmptyGlobalGroup()
    {
        var store = new MappingStore();

        var global = Assert.Single(store.Current.Groups);
        Assert.True(global.IsGlobal);
        Assert.Same(global, store.Global);
        Assert.Equal("Global", global.Name);
        Assert.Empty(global.Commands);
        Assert.Empty(store.Current.Ignored);
        Assert.Equal(0, store.Version);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void AddGroupStoresItTrimmedInNameOrderAndRaisesChanged()
    {
        var store = new MappingStore();
        int raised = 0;
        store.Changed += (_, _) => raised++;
        store.AddGroup(NewGroup("Zeta"));

        var stored = store.AddGroup(NewGroup("  alpha "));

        Assert.Equal("alpha", stored.Name);
        Assert.Same(stored, store.FindGroup(stored.Id));
        Assert.Equal(["Global", "alpha", "Zeta"], store.Current.Groups.Select(group => group.Name));
        Assert.Equal(2, raised);
        Assert.Equal(2, store.Version);
        Assert.True(store.CanUndo);
    }

    [Fact]
    public void ARejectedMutationLeavesTheStoreUntouched()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Chrome")));
        var before = store.Current;

        Assert.Throws<MappingValidationException>(() => store.AddGroup(NewGroup("chrome")));

        Assert.Same(before, store.Current);
        Assert.Equal(0, store.Version);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void UpdateGroupReplacesByIdAndRejectsUnknownIds()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Chrome", commands: [NewCommand("Close tab", Up)])));
        var chrome = store.Current.Groups[1];

        var updated = store.UpdateGroup(chrome with { Name = "Chromium", SuppressGlobals = true });

        Assert.Equal("Chromium", updated.Name);
        Assert.True(updated.SuppressGlobals);
        Assert.Equal("Close tab", Assert.Single(updated.Commands).Name);
        Assert.Throws<KeyNotFoundException>(() => store.UpdateGroup(chrome with { Id = GroupId.New() }));
    }

    [Fact]
    public void RemoveGroupRefusesGlobalAndUndoRestoresTheRest()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Chrome", commands: [NewCommand("Close tab", Up)])));
        var chrome = store.Current.Groups[1];

        var refused = Assert.Throws<MappingValidationException>(() => store.RemoveGroup(GroupId.Global));
        Assert.Equal("The Global group cannot be removed.", refused.Message);

        var removed = store.RemoveGroup(chrome.Id);

        Assert.Same(chrome, removed);
        Assert.Single(store.Current.Groups);
        Assert.Throws<KeyNotFoundException>(() => store.RemoveGroup(chrome.Id));

        Assert.True(store.Undo());

        Assert.Equal(chrome.Id, store.Current.Groups[1].Id);
        Assert.Equal("Close tab", Assert.Single(store.Current.Groups[1].Commands).Name);
    }

    [Fact]
    public void AddCommandSortsWithinTheGroupAndEnforcesOneCommandPerTrigger()
    {
        var store = new MappingStore();
        store.AddCommand(GroupId.Global, NewCommand("Minimize", Down));

        var close = store.AddCommand(GroupId.Global, NewCommand(" Close ", Up, NewStep("close")));

        Assert.Equal("Close", close.Name);
        Assert.Equal(["Close", "Minimize"], store.Global.Commands.Select(command => command.Name));
        Assert.Throws<MappingValidationException>(() => store.AddCommand(GroupId.Global, NewCommand("Again", Up)));
        Assert.Throws<KeyNotFoundException>(() => store.AddCommand(GroupId.New(), NewCommand("Lost")));
        Assert.Equal(2, store.Version);
    }

    [Fact]
    public void UpdateCommandReplacesByIdInThatGroupAndResorts()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Alpha", Up), NewCommand("Beta", Down))));
        var alpha = store.Global.Commands[0];

        var renamed = store.UpdateCommand(GroupId.Global, alpha with { Name = "Zulu", IsActive = false });

        Assert.Equal(alpha.Id, renamed.Id);
        Assert.False(renamed.IsActive);
        Assert.Equal(["Beta", "Zulu"], store.Global.Commands.Select(command => command.Name));
        Assert.Throws<MappingValidationException>(() => store.UpdateCommand(GroupId.Global, renamed with { Trigger = Trigger.ForGesture(Down) }));
        Assert.Throws<KeyNotFoundException>(() => store.UpdateCommand(GroupId.Global, renamed with { Id = CommandId.New() }));
    }

    [Fact]
    public void RemoveCommandThenUndoBringsItBackWithTheSameId()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Close", Up)), NewGroup("Chrome", commands: [NewCommand("Close tab", Up)])));
        var closeTab = store.Current.Groups[1].Commands[0];

        var removed = store.RemoveCommand(closeTab.Id);

        Assert.Same(closeTab, removed);
        Assert.Empty(store.Current.Groups[1].Commands);
        Assert.Null(store.FindCommand(closeTab.Id));
        Assert.Throws<KeyNotFoundException>(() => store.RemoveCommand(closeTab.Id));

        Assert.True(store.Undo());

        Assert.Equal(closeTab.Id, store.FindCommand(closeTab.Id)!.Value.Command.Id);
    }

    [Fact]
    public void MoveCommandPastesIntoAnotherGroupAsOneUndoStep()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Close", Up)), NewGroup("Chrome"), NewGroup("Edge", commands: [NewCommand("Close", Down)])));
        var close = store.Global.Commands[0];
        var chrome = store.Current.Groups[1];
        var edge = store.Current.Groups[2];
        int version = store.Version;

        var moved = store.MoveCommand(close.Id, chrome.Id);

        Assert.Equal(close.Id, moved.Id);
        Assert.Empty(store.Global.Commands);
        Assert.Equal(chrome.Id, store.FindCommand(close.Id)!.Value.Group.Id);
        Assert.Equal(version + 1, store.Version);

        Assert.Same(moved, store.MoveCommand(close.Id, chrome.Id));
        Assert.Equal(version + 1, store.Version);
        Assert.Throws<MappingValidationException>(() => store.MoveCommand(close.Id, edge.Id));
        Assert.Throws<KeyNotFoundException>(() => store.MoveCommand(close.Id, GroupId.New()));

        Assert.True(store.Undo());
        Assert.True(store.Global.FindCommand(close.Id) is not null);
    }

    [Fact]
    public void UsedByListsEveryCommandBoundToTheGestureInDocumentOrder()
    {
        var store = new MappingStore(Document(
            NewGroup("Steam", commands: [NewCommand("Nothing", Up)]),
            NewGlobal(NewCommand("Close", Up), NewCommand("Volume", Trigger.ForWheel(WheelDirection.Up))),
            NewGroup("Chrome", commands: [NewCommand("Close tab", Up), NewCommand("Back", Down)])));

        var used = store.UsedBy(Up);

        Assert.Equal(["Global › Close", "Chrome › Close tab", "Steam › Nothing"], used.Select(pair => $"{pair.Group.Name} › {pair.Command.Name}"));
        Assert.Equal(["Chrome › Back"], store.UsedBy(Down).Select(pair => $"{pair.Group.Name} › {pair.Command.Name}"));
        Assert.Empty(store.UsedBy(Core.Gestures.GestureId.New()));
    }

    [Fact]
    public void FindCommandReturnsTheGroupWithTheCommand()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Chrome", commands: [NewCommand("Close tab", Up)])));
        var closeTab = store.Current.Groups[1].Commands[0];

        var found = store.FindCommand(closeTab.Id);

        Assert.NotNull(found);
        Assert.Equal("Chrome", found.Value.Group.Name);
        Assert.Same(closeTab, found.Value.Command);
        Assert.Null(store.FindCommand(CommandId.New()));
    }

    [Fact]
    public void IgnoredAppsAreAddedUpdatedAndRemoved()
    {
        var store = new MappingStore();
        var game = new IgnoredApp(GroupId.New(), " Game ", true, ByProcess("game.exe"), DisableEntirely: false);

        var added = store.AddIgnored(game);
        Assert.Equal("Game", added.Name);
        Assert.Same(added, store.FindIgnored(game.Id));

        var updated = store.UpdateIgnored(added with { DisableEntirely = true });
        Assert.True(updated.DisableEntirely);
        Assert.Throws<KeyNotFoundException>(() => store.UpdateIgnored(added with { Id = GroupId.New() }));

        Assert.Same(updated, store.RemoveIgnored(game.Id));
        Assert.Empty(store.Current.Ignored);
        Assert.Throws<KeyNotFoundException>(() => store.RemoveIgnored(game.Id));
        Assert.Equal(3, store.Version);
    }

    [Fact]
    public void ReplaceAllIsOneUndoStepAndValidatesTheWholeDocument()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Chrome")));

        var imported = store.ReplaceAll(Document(NewGroup("B"), NewGlobal(NewCommand("Close", Up)), NewGroup("a")));

        Assert.Equal(["Global", "a", "B"], imported.Groups.Select(group => group.Name));
        Assert.Throws<MappingValidationException>(() => store.ReplaceAll(Document(NewGroup("No global"))));
        Assert.Same(imported, store.Current);
        Assert.True(store.Undo());
        Assert.Equal(["Global", "Chrome"], store.Current.Groups.Select(group => group.Name));
    }

    [Fact]
    public void UndoRedoWalkBothWaysAndANewChangeDropsRedo()
    {
        var store = new MappingStore();
        var chrome = store.AddGroup(NewGroup("Chrome"));
        store.AddCommand(chrome.Id, NewCommand("Close tab", Up));
        store.UpdateGroup(store.FindGroup(chrome.Id)! with { Name = "Chromium" });

        Assert.True(store.Undo());
        Assert.Equal("Chrome", store.FindGroup(chrome.Id)!.Name);
        Assert.True(store.Undo());
        Assert.Empty(store.FindGroup(chrome.Id)!.Commands);
        Assert.True(store.Redo());
        Assert.Single(store.FindGroup(chrome.Id)!.Commands);
        Assert.True(store.CanRedo);

        store.AddIgnored(new IgnoredApp(GroupId.New(), "Game", true, ByProcess("game.exe"), false));

        Assert.False(store.CanRedo);
        Assert.False(store.Redo());
        Assert.Equal(7, store.Version);
        store.ClearHistory();
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void CurrentIsASnapshotThatDoesNotChangeUnderTheCaller()
    {
        var store = new MappingStore();
        var before = store.Current;

        store.AddGroup(NewGroup("Chrome"));

        Assert.Single(before.Groups);
        Assert.Equal(2, store.Current.Groups.Count);
    }
}
