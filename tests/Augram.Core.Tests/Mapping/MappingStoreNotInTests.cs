using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// The store's side of the "Not in" dialog (plan 0004 step 7), and of the "Also in" dialog's ✕ (Joel, 2026-10-12): new Ignored ›
/// Per command entries, removed unused ones and the command naming them are one change and one undo step, refused as a whole;
/// moving an entry to Global drops it from every "Not in" in that step; and <see cref="MappingDocument.UsersOf"/> names an
/// entry's users for both kinds.
/// </summary>
public sealed class MappingStoreNotInTests
{
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));

    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    private static IgnoredApp Excluded(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false);

    [Fact]
    public void UsersOf_NamesACommandThroughItsNotInForAPerCommandEntry_AndThroughItsAlsoInForAGlobalEntry()
    {
        var spine = PerCommand("Spine");
        var blender = Excluded("Blender");
        var krita = Excluded("Krita");
        var zoomIn = NewCommand("Zoom In") with { NotIn = [spine.Id] };
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(zoomIn, magnifier)], [spine, blender, krita]));

        Assert.Equal(["Zoom In"], mapping.UsersOf(spine.Id).Select(pair => pair.Command.Name));
        Assert.Equal(["Magnifier"], mapping.UsersOf(blender.Id).Select(pair => pair.Command.Name));
        Assert.Empty(mapping.UsersOf(krita.Id));
    }

    /// <summary>The Also in dialog's Save (Joel, 2026-10-12): a Global entry ✕ took away goes with the command's change, unless a command's Also in still names it.</summary>
    [Fact]
    public void RemovedGlobalEntries_GoWithTheCommandsChange_ButNotOneACommandsAlsoInStillNames()
    {
        var blender = Excluded("Blender");
        var krita = Excluded("Krita");
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] };
        var zoom = NewCommand("Zoom", Trigger.ForButton(MouseButton.Middle, new TriggerHold(HeldButtons.Right)), NewStep("zoom")) with { AlsoIn = [krita.Id] };
        var store = new MappingStore(new MappingDocument([NewGlobal(magnifier, zoom)], [blender, krita]));
        var before = store.Current;
        var stored = store.Global.Commands.Single(command => command.Name == "Magnifier");

        // Krita is Zoom's; Blender is still ticked on Magnifier: both refused, nothing stored.
        var refused = Assert.Throws<MappingValidationException>(() => store.UpdateCommandChangingIgnored(GroupId.Global, stored, [], [krita.Id]));
        Assert.Equal("'Krita' is still used in a command's Also in, so it cannot be removed.", refused.Message);
        Assert.Throws<MappingValidationException>(() => store.UpdateCommandChangingIgnored(GroupId.Global, stored, [], [blender.Id]));
        Assert.Same(before, store.Current);

        store.UpdateCommandChangingIgnored(GroupId.Global, stored with { AlsoIn = [] }, [], [blender.Id]);

        Assert.Equal([krita.Id], store.Current.Ignored.Select(app => app.Id));
        Assert.Empty(store.Global.Commands.Single(command => command.Name == "Magnifier").AlsoIn);
        Assert.True(store.Undo());
        Assert.Same(before, store.Current);
    }

    [Fact]
    public void NewEntriesAndTheCommandNamingThem_AreOneUndoStep()
    {
        var store = new MappingStore(Document(NewGlobal(NewCommand("Zoom In"))));
        var zoom = store.Global.Commands[0];
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");

        var stored = store.UpdateCommandChangingIgnored(GroupId.Global, zoom with { NotIn = [spine.Id, eyeris.Id] }, [spine, eyeris], []);

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

        Assert.Throws<MappingValidationException>(() => store.UpdateCommandChangingIgnored(GroupId.Global, store.Global.Commands[0] with { NotIn = [clash.Id] }, [clash], []));

        Assert.Same(before, store.Current);
        Assert.False(store.CanUndo);
    }

    [Fact]
    public void RemovedEntries_GoWithTheCommandsChange_InOneUndoStep_ButNotOneACommandStillNames()
    {
        var spine = PerCommand("Spine");
        var krita = PerCommand("Krita");
        var eyeris = PerCommand("Eyeris");
        var zoomIn = NewCommand("Zoom In") with { NotIn = [spine.Id] };
        var zoomOut = NewCommand("Zoom Out") with { NotIn = [eyeris.Id] };
        var store = new MappingStore(new MappingDocument([NewGlobal(zoomIn, zoomOut)], [spine, krita, eyeris]));
        var before = store.Current;
        var stored = store.Global.Commands.Single(command => command.Name == "Zoom In");

        // Eyeris is Zoom Out's; Spine is still ticked on Zoom In: both refused, nothing stored.
        Assert.Throws<MappingValidationException>(() => store.UpdateCommandChangingIgnored(GroupId.Global, stored, [], [eyeris.Id]));
        Assert.Throws<MappingValidationException>(() => store.UpdateCommandChangingIgnored(GroupId.Global, stored, [], [spine.Id]));
        Assert.Same(before, store.Current);

        store.UpdateCommandChangingIgnored(GroupId.Global, stored with { NotIn = [] }, [], [spine.Id, krita.Id]);

        Assert.Equal([eyeris.Id], store.Current.Ignored.Select(app => app.Id));
        Assert.Empty(store.Global.Commands.Single(command => command.Name == "Zoom In").NotIn);
        Assert.True(store.Undo());
        Assert.Same(before, store.Current);
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
