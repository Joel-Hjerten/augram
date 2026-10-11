using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Categories through the store: edited with <see cref="MappingStore.UpdateGroup"/>, cleared on commands that lose theirs (on
/// Global: into Uncategorized, Joel 2026-10-11), carried by name on a move.
/// </summary>
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

        Assert.Equal([Media, Uncategorized], updated.Categories);
        Assert.Equal(Media.Id, updated.Commands[0].CategoryId);
        Assert.Equal(version + 1, store.Version);
        Assert.True(store.Undo());
        Assert.Equal([Uncategorized], store.Global.Categories);
        Assert.Equal(Uncategorized.Id, store.Global.Commands[0].CategoryId);
    }

    [Fact]
    public void DeletingACategoryMovesItsCommandsIntoUncategorized_MadeWhenThereIsNone()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play").In(Media), NewCommand("Close").In(Window)) with { Categories = [Media, Window] }));

        var updated = store.UpdateGroup(store.Global with { Categories = [Window] });

        Assert.Equal([Uncategorized, Window], updated.Categories);
        Assert.Equal([Window.Id, Uncategorized.Id], updated.Commands.Select(command => command.CategoryId));
    }

    [Fact]
    public void AnEmptiedUncategorizedStays_LikeAnyCategory_UntilItIsDeleted()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play")) with { Categories = [Media] }));

        var emptied = store.UpdateGroup(store.Global with { Commands = [store.Global.Commands[0].In(Media)] });
        var deleted = store.UpdateGroup(emptied with { Categories = [Media] });

        Assert.Equal([Media, Uncategorized], emptied.Categories);
        Assert.Equal([Media], deleted.Categories);
    }

    [Fact]
    public void DeletingUncategorizedWithItsCommands_LeavesNoneBehind()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play"), NewCommand("Close").In(Media)) with { Categories = [Media] }));
        var global = store.Global;

        var updated = store.UpdateGroup(global with
        {
            Categories = [Media],
            Commands = [.. global.Commands.Where(command => command.CategoryId != Uncategorized.Id)],
        });

        Assert.Equal([Media], updated.Categories);
        Assert.Equal(["Close"], updated.Commands.Select(command => command.Name));
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
    public void AddAndUpdateCommandClearACategoryAnAppGroupLacks()
    {
        var chrome = NewGroup("Chrome") with { Categories = [Media] };
        var store = new MappingStore(Document(NewGlobal(), chrome));

        var added = store.AddCommand(chrome.Id, NewCommand("Close").In(Window));
        var kept = store.UpdateCommand(chrome.Id, added.In(Media));
        var cleared = store.UpdateCommand(chrome.Id, kept.In(Window));

        Assert.Null(added.CategoryId);
        Assert.Equal(Media.Id, kept.CategoryId);
        Assert.Null(cleared.CategoryId);
        Assert.Equal([Media], store.FindGroup(chrome.Id)!.Categories);
    }

    [Fact]
    public void OnGlobal_AddAndUpdateCommandPutACategoryTheGroupLacks_IntoUncategorized()
    {
        var store = new MappingStore(Document(NewGlobal() with { Categories = [Media] }));

        var added = store.AddCommand(GroupId.Global, NewCommand("Close").In(Window));
        var kept = store.UpdateCommand(GroupId.Global, added.In(Media));
        var homeless = store.UpdateCommand(GroupId.Global, kept with { CategoryId = null });

        Assert.Equal(Uncategorized.Id, added.CategoryId);
        Assert.Equal(Media.Id, kept.CategoryId);
        Assert.Equal(Uncategorized.Id, homeless.CategoryId);
        Assert.Equal([Media, Uncategorized], store.Global.Categories);
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
        Assert.Equal([Media, Uncategorized, Window], store.Global.Categories);
    }

    [Fact]
    public void ACommandMovedIntoGlobal_WithoutACategoryOfTheSameName_LandsInUncategorized()
    {
        var theirTools = NewCategory("Tools");
        var chrome = NewGroup("Chrome", null, NewCommand("Reload").In(theirTools), NewCommand("Loose")) with { Categories = [theirTools] };
        var store = new MappingStore(Document(NewGlobal(NewCommand("Play").In(Media)) with { Categories = [Media] }, chrome));

        var reload = store.MoveCommand(chrome.Commands[0].Id, GroupId.Global);
        var loose = store.MoveCommand(chrome.Commands[1].Id, GroupId.Global);

        Assert.Equal(Uncategorized.Id, reload.CategoryId);
        Assert.Equal(Uncategorized.Id, loose.CategoryId);
        Assert.Equal([Media, Uncategorized], store.Global.Categories);
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
