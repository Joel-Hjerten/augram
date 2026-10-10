using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Rebind and the add-only merge: groups by name, Global into Global, skipped commands counted, ignored apps by name.</summary>
public sealed class MappingImportTests
{
    private static readonly GestureId G1 = GestureId.New();
    private static readonly GestureId G2 = GestureId.New();
    private static readonly GestureId G3 = GestureId.New();

    private static Command Cmd(string name, Trigger trigger, bool isActive = true) => new(CommandId.New(), name, trigger, isActive, []);

    private static AppGroup GroupOf(string name, bool suppressGlobals, params Command[] commands)
        => new(GroupId.New(), name, IsActive: true, suppressGlobals, new AppMatcher { WindowsProcessNames = [name.ToLowerInvariant() + ".exe"] }, commands);

    private static AppGroup GlobalOf(params Command[] commands) => AppGroup.EmptyGlobal with { Commands = commands };

    private static IgnoredApp Ign(string name) => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = ["x.exe"] }, DisableEntirely: false);

    private static MappingDocument Doc(IReadOnlyList<AppGroup> groups, params IgnoredApp[] ignored) => new(groups, ignored);

    [Fact]
    public void RebindReplacesMappedGestureTriggersOnly()
    {
        var mapped = Cmd("Mapped", Trigger.ForGesture(G1));
        var unmapped = Cmd("Unmapped", Trigger.ForGesture(G3));
        var wheel = Cmd("Wheel", Trigger.ForWheel(WheelDirection.Up));
        var none = Cmd("None", Trigger.None);
        var document = Doc([GlobalOf(mapped, unmapped, wheel, none)]);

        var rebound = MappingImport.Rebind(document, new Dictionary<GestureId, GestureId> { [G1] = G2, [G3] = G3 });

        var commands = rebound.Global.Commands;
        Assert.Equal(Trigger.ForGesture(G2), commands[0].Trigger);
        Assert.Equal(Trigger.ForGesture(G3), commands[1].Trigger);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), commands[2].Trigger);
        Assert.Equal(Trigger.None, commands[3].Trigger);
        Assert.Equal(mapped.Id, commands[0].Id);
        Assert.Equal(Trigger.ForGesture(G1), document.Global.Commands[0].Trigger);
    }

    [Fact]
    public void GlobalMergesIntoGlobal()
    {
        var existing = MappingRules.ValidDocument(Doc([GlobalOf(Cmd("A", Trigger.ForGesture(G1)))]));
        var imported = Doc([GlobalOf(Cmd("B", Trigger.ForGesture(G2)))]);

        var result = MappingImport.Merge(existing, imported);

        Assert.Single(result.Document.Groups);
        Assert.Equal(["A", "B"], result.Document.Global.Commands.Select(command => command.Name));
        Assert.Equal(0, result.GroupsAdded);
        Assert.Equal(1, result.CommandsAdded);
        Assert.Equal(0, result.CommandsSkipped);
    }

    [Fact]
    public void GroupsMergeByNameCaseInsensitivelyAndKeepTheExistingDefinition()
    {
        var mine = GroupOf("Chrome", suppressGlobals: true, Cmd("Close", Trigger.ForGesture(G1)));
        var existing = MappingRules.ValidDocument(Doc([AppGroup.EmptyGlobal, mine]));
        var theirs = new AppGroup(GroupId.New(), "chrome", IsActive: false, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["other.exe"] }, [Cmd("Back", Trigger.ForGesture(G2))]);

        var result = MappingImport.Merge(existing, Doc([AppGroup.EmptyGlobal, theirs]));

        var group = Assert.Single(result.Document.Groups, group => !group.IsGlobal);
        Assert.Equal(mine.Id, group.Id);
        Assert.Equal("Chrome", group.Name);
        Assert.True(group.SuppressGlobals);
        Assert.True(group.IsActive);
        Assert.Equal(["chrome.exe"], group.Matcher!.WindowsProcessNames);
        Assert.Equal(["Back", "Close"], group.Commands.Select(command => command.Name));
        Assert.Equal(0, result.GroupsAdded);
        Assert.Equal(1, result.CommandsAdded);
    }

    [Fact]
    public void NewGroupIsAddedWholeAndSortedIntoPlace()
    {
        var existing = MappingRules.ValidDocument(Doc([AppGroup.EmptyGlobal, GroupOf("Zed", false)]));
        var steam = GroupOf("Steam", false, Cmd("D", Trigger.ForGesture(G1)), Cmd("E", Trigger.None));

        var result = MappingImport.Merge(existing, Doc([AppGroup.EmptyGlobal, steam]));

        Assert.Equal(["Global", "Steam", "Zed"], result.Document.Groups.Select(group => group.Name));
        Assert.Equal(steam.Id, result.Document.Groups[1].Id);
        Assert.Equal(1, result.GroupsAdded);
        Assert.Equal(2, result.CommandsAdded);
    }

    [Fact]
    public void CommandWithAnExistingNameIsSkipped()
    {
        var close = Cmd("Close", Trigger.ForGesture(G1));
        var existing = MappingRules.ValidDocument(Doc([GlobalOf(close)]));

        var result = MappingImport.Merge(existing, Doc([GlobalOf(Cmd("close", Trigger.ForGesture(G2)))]));

        Assert.Equal(close, Assert.Single(result.Document.Global.Commands));
        Assert.Equal(0, result.CommandsAdded);
        Assert.Equal(1, result.CommandsSkipped);
    }

    /// <summary>Command names are unique within their parent (Joel, 2026-10-10): a command under a hold remap does not hold the name for an imported, ordinary one.</summary>
    [Fact]
    public void CommandNamedLikeOneUnderAHoldRemapIsAdded()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var orbit = Cmd("Orbit", Trigger.ForInput(HoldInput.Of(MouseButton.Left))) with { HoldRemapId = space.Id };
        var existing = MappingRules.ValidDocument(Doc([AppGroup.EmptyGlobal, GroupOf("Blender", false, orbit) with { HoldRemaps = [space] }]));

        var result = MappingImport.Merge(existing, Doc([AppGroup.EmptyGlobal, GroupOf("Blender", false, Cmd("orbit", Trigger.ForGesture(G1)))]));

        var blender = result.Document.Groups[1];
        Assert.Equal(2, blender.Commands.Count);
        Assert.Single(blender.Commands, command => command.Name == "orbit" && command.HoldRemapId is null);
        Assert.Equal(1, result.CommandsAdded);
        Assert.Equal(0, result.CommandsSkipped);
    }

    [Fact]
    public void CommandWhoseTriggerIsAlreadyBoundIsSkipped()
    {
        var existing = MappingRules.ValidDocument(Doc([GlobalOf(Cmd("Close", Trigger.ForGesture(G1)), Cmd("Louder", Trigger.ForWheel(WheelDirection.Up)))]));
        var imported = GlobalOf(
            Cmd("Shut", Trigger.ForGesture(G1)),
            Cmd("Volume Up", Trigger.ForWheel(WheelDirection.Up)),
            Cmd("Unbound One", Trigger.None),
            Cmd("Unbound Two", Trigger.None));

        var result = MappingImport.Merge(existing, Doc([imported]));

        Assert.Equal(["Close", "Louder", "Unbound One", "Unbound Two"], result.Document.Global.Commands.Select(command => command.Name));
        Assert.Equal(2, result.CommandsAdded);
        Assert.Equal(2, result.CommandsSkipped);
    }

    [Fact]
    public void ImportedCommandsClashingAmongThemselvesAfterRebindKeepTheFirst()
    {
        var imported = GlobalOf(Cmd("First", Trigger.ForGesture(G1)), Cmd("Second", Trigger.ForGesture(G2)));
        var rebound = MappingImport.Rebind(Doc([imported]), new Dictionary<GestureId, GestureId> { [G2] = G1 });

        var result = MappingImport.Merge(MappingDocument.Empty, rebound);

        Assert.Equal(["First"], result.Document.Global.Commands.Select(command => command.Name));
        Assert.Equal(1, result.CommandsSkipped);
    }

    [Fact]
    public void IgnoredAppsMergeByName()
    {
        var vm = Ign("VM");
        var existing = MappingRules.ValidDocument(Doc([AppGroup.EmptyGlobal], vm));

        var result = MappingImport.Merge(existing, Doc([AppGroup.EmptyGlobal], Ign("vm"), Ign("Game")));

        Assert.Equal([vm.Id], result.Document.Ignored.Take(1).Select(app => app.Id));
        Assert.Equal(["VM", "Game"], result.Document.Ignored.Select(app => app.Name));
        Assert.Equal(1, result.IgnoredAdded);
        Assert.Equal(1, result.IgnoredSkipped);
    }

    [Fact]
    public void MergeDoesNotMutateItsInputs()
    {
        var existing = MappingRules.ValidDocument(Doc([GlobalOf(Cmd("A", Trigger.None))]));
        var imported = Doc([GlobalOf(Cmd("B", Trigger.None)), GroupOf("New", false)]);

        MappingImport.Merge(existing, imported);

        Assert.Single(existing.Global.Commands);
        Assert.Single(existing.Groups);
        Assert.Single(imported.Global.Commands);
    }

    [Fact]
    public void MergeIntoADocumentWithoutGlobalAddsOne()
    {
        var result = MappingImport.Merge(new MappingDocument([], []), Doc([GlobalOf(Cmd("A", Trigger.None))]));

        Assert.Equal(["A"], result.Document.Global.Commands.Select(command => command.Name));
        Assert.Equal(0, result.GroupsAdded);
    }
}
