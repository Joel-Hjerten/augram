using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>The Gestures tab's edits on the mapping (<see cref="MappingStore.UnbindGesture"/>, <see cref="MappingStore.RetargetGesture"/>): one undo step per action (plan 0007 Q8).</summary>
public sealed class MappingStoreGestureTests
{
    [Fact]
    public void UnbindingAGestureUnbindsEveryCommandOnItAsOneUndoStep()
    {
        var store = new MappingStore(Document(
            NewGlobal(NewCommand("Minimize", Up)),
            NewGroup("Chrome", null, NewCommand("Close tab", Up), NewCommand("Reload", Down))));

        var unbound = store.UnbindGesture(Up);

        Assert.Equal(2, unbound);
        Assert.False(Command(store, "Minimize").Trigger.IsBound);
        Assert.False(Command(store, "Close tab").Trigger.IsBound);
        Assert.True(Command(store, "Reload").UsesGesture(Down));
        Assert.Equal(1, store.Version);
        Assert.True(store.Undo());
        Assert.True(Command(store, "Minimize").UsesGesture(Up));
        Assert.True(Command(store, "Close tab").UsesGesture(Up));
        Assert.False(store.CanUndo);
        Assert.Equal(0, new MappingStore().UnbindGesture(Up));
    }

    [Fact]
    public void RetargetingMovesCommandsToTheKeptGesture_UnbindsOneWhoseGroupHasItAlready_AsOneUndoStep()
    {
        var store = new MappingStore(Document(
            NewGlobal(NewCommand("Minimize", Down)),
            NewGroup("Chrome", null, NewCommand("Close tab", Up), NewCommand("Reload", Down))));

        var (retargeted, unbound) = store.RetargetGesture([Down], Up);

        Assert.Equal((1, 1), (retargeted, unbound));
        Assert.True(Command(store, "Minimize").UsesGesture(Up));
        Assert.False(Command(store, "Reload").Trigger.IsBound);
        Assert.True(Command(store, "Close tab").UsesGesture(Up));
        Assert.Equal(1, store.Version);
        Assert.True(store.Undo());
        Assert.True(Command(store, "Minimize").UsesGesture(Down));
        Assert.True(Command(store, "Reload").UsesGesture(Down));
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void TheGlobalGroupCannotBeRenamed_ItsCategoriesCan()
    {
        var media = NewCategory("Media");
        var store = new MappingStore(Document(NewGlobal() with { Categories = [media] }));

        var refused = Assert.Throws<MappingValidationException>(() => store.UpdateGroup(store.Global with { Name = "Everywhere" }));
        Assert.Equal("The Global group cannot be renamed.", refused.Message);

        var renamed = store.UpdateGroup(store.Global with { Categories = [media with { Name = "Audio" }] });
        Assert.Equal("Audio", Assert.Single(renamed.Categories).Name);
        Assert.Equal("Global", renamed.Name);
    }

    private static Command Command(MappingStore store, string name)
        => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;
}
