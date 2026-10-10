using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// The store's side of the "Not in" dialog (plan 0004 step 7): new Ignored › Per command entries and the command naming them
/// are one change and one undo step, refused as a whole; moving an entry to Global drops it from every "Not in" in that step.
/// </summary>
public sealed class MappingStoreNotInTests
{
    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    [Fact]
    public void NewEntriesAndTheCommandNamingThem_AreOneUndoStep()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Zoom In"))));
        var zoom = store.Global.Commands[0];
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");

        var stored = store.UpdateCommandAddingIgnored(GroupId.Global, zoom with { NotIn = [spine.Id, eyeris.Id] }, [spine, eyeris]);

        Assert.Equal(new[] { spine.Id, eyeris.Id }.OrderBy(id => id.Value), stored.NotIn);
        Assert.Equal(["Spine", "Eyeris"], store.Current.Ignored.Select(app => app.Name));
        Assert.All(store.Current.Ignored, app => Assert.True(app.IsPerCommand));

        Assert.True(store.Undo());
        Assert.Empty(store.Current.Ignored);
        Assert.Empty(store.Global.Commands[0].NotIn);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void ARefusedPart_LeavesEverythingAsItWas()
    {
        var spine = PerCommand("Spine");
        var store = new MappingStore(new MappingDocument([NewGlobal(NewCommand("Zoom In"))], [spine]));
        var before = store.Current;
        var clash = PerCommand("spine");

        Assert.Throws<MappingValidationException>(() => store.UpdateCommandAddingIgnored(GroupId.Global, store.Global.Commands[0] with { NotIn = [clash.Id] }, [clash]));

        Assert.Same(before, store.Current);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void MovingAnEntryToGlobal_DropsItFromEveryNotIn_InOneUndoStep()
    {
        var spine = PerCommand("Spine");
        var zoom = NewCommand("Zoom In") with { NotIn = [spine.Id] };
        var store = new MappingStore(new MappingDocument([NewGlobal(zoom)], [spine]));

        store.UpdateIgnored(spine with { Scope = IgnoreScope.Global });

        Assert.False(store.FindIgnored(spine.Id)!.IsPerCommand);
        Assert.Empty(store.Global.Commands[0].NotIn);

        store.Undo();
        Assert.True(store.FindIgnored(spine.Id)!.IsPerCommand);
        Assert.Equal([spine.Id], store.Global.Commands[0].NotIn);
    }
}
