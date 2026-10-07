using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>Categories through the store: edited with <see cref="MappingStore.UpdateGroup"/>, cleared on commands that lose theirs, carried by name on a move.</summary>
public sealed class MappingStoreCategoryTests
{
    private static readonly CommandCategory Media = NewCategory("Media");
    private static readonly CommandCategory Window = NewCategory("Window");

    [Fact]
    public void AddingACategoryAndSortingACommandIntoItIsOneUndoStep()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play"))));
        var global = store.Global;
        int version = store.Version;

        var updated = store.UpdateGroup(global with
        {
            Categories = [.. global.Categories, Media],
            Commands = [global.Commands[0].In(Media)],
        });

        Assert.Equal(Media, Assert.Single(updated.Categories));
        Assert.Equal(Media.Id, updated.Commands[0].CategoryId);
        Assert.Equal(version + 1, store.Version);
        Assert.True(store.Undo());
        Assert.Empty(store.Global.Categories);
        Assert.Null(store.Global.Commands[0].CategoryId);
    }

    [Fact]
    public void DeletingACategoryLeavesItsCommandsUncategorized()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play").In(Media), NewCommand("Close").In(Window)) with { Categories = [Media, Window] }));

        var updated = store.UpdateGroup(store.Global with { Categories = [Window] });

        Assert.Equal([Window.Id, null], updated.Commands.Select(command => command.CategoryId));
    }

    [Fact]
    public void ADuplicateCategoryNameIsRefusedAndTheStoreIsUnchanged()
    {
        var store = new MappingStore(Document(NewGlobal() with { Categories = [Media] }));
        var before = store.Current;

        var ex = Assert.Throws<MappingValidationException>(() => store.UpdateGroup(store.Global with { Categories = [Media, NewCategory("MEDIA")] }));

        Assert.Equal("A category named 'Media' already exists in 'Global'.", ex.Message);
        Assert.Same(before, store.Current);
    }

    [Fact]
    public void AddAndUpdateCommandClearACategoryTheGroupLacks()
    {
        var store = new MappingStore(Document(NewGlobal() with { Categories = [Media] }));

        var added = store.AddCommand(GroupId.Global, NewCommand("Close").In(Window));
        var kept = store.UpdateCommand(GroupId.Global, added.In(Media));
        var cleared = store.UpdateCommand(GroupId.Global, kept.In(Window));

        Assert.Null(added.CategoryId);
        Assert.Equal(Media.Id, kept.CategoryId);
        Assert.Null(cleared.CategoryId);
    }

    [Fact]
    public void MoveCommandTakesTheTargetCategoryOfTheSameNameOrNone()
    {
        var theirMedia = NewCategory("media");
        var store = new MappingStore(Document(
            NewGlobal(NewCommand("Play").In(Media), NewCommand("Close").In(Window), NewCommand("Loose")) with { Categories = [Media, Window] },
            NewGroup("Chrome") with { Categories = [theirMedia] }));
        var chrome = store.Current.Groups[1];

        var play = store.MoveCommand(store.Global.Commands.Single(command => command.Name == "Play").Id, chrome.Id);
        var close = store.MoveCommand(store.Global.Commands.Single(command => command.Name == "Close").Id, chrome.Id);
        var loose = store.MoveCommand(store.Global.Commands.Single(command => command.Name == "Loose").Id, chrome.Id);

        Assert.Equal(theirMedia.Id, play.CategoryId);
        Assert.Null(close.CategoryId);
        Assert.Null(loose.CategoryId);
        Assert.Equal([theirMedia], store.FindGroup(chrome.Id)!.Categories);
        Assert.Equal([Media, Window], store.Global.Categories);
    }

    [Fact]
    public void UndoOfAMoveRestoresTheOriginalCategory()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play").In(Media)) with { Categories = [Media] }, NewGroup("Chrome")));
        var play = store.Global.Commands[0];

        Assert.Null(store.MoveCommand(play.Id, store.Current.Groups[1].Id).CategoryId);
        Assert.True(store.Undo());

        Assert.Equal(Media.Id, store.Global.FindCommand(play.Id)!.CategoryId);
    }
}
