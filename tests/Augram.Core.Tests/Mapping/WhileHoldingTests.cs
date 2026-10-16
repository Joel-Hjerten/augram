using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Exclusions › While holding (plan 0012): an entry's inputs per platform by a command's side rules, the store's edits (one undo
/// step each), the rules, the hold-key conflict query and the inputs over a window the engine reads.
/// </summary>
public sealed class WhileHoldingTests
{
    private static readonly KeyOrButton Ctrl = new KeyOrButton.Modifier(KeyModifiers.Control);
    private static readonly KeyOrButton Cmd = new KeyOrButton.Modifier(KeyModifiers.Meta);
    private static readonly KeyOrButton Alt = new KeyOrButton.Modifier(KeyModifiers.Alt);
    private static readonly KeyOrButton S = new KeyOrButton.Key(KeyCode.S);
    private static readonly KeyOrButton Space = new KeyOrButton.Key(KeyCode.Space);
    private static readonly KeyOrButton Middle = new KeyOrButton.Button(MouseButton.Middle);

    [Fact]
    public void TheInferredSideFollowsTheOriginalConverted_CtrlBecomesCmd_KeysAndButtonsStay_WinCountsForNothing()
    {
        var entry = WhileHolding("After Effects", Ctrl, S, Middle, new KeyOrButton.Modifier(KeyModifiers.Meta));

        Assert.True(entry.Holding.IsInferredOn(HostPlatform.MacOS));
        Assert.Equal([Ctrl, S, Middle, Cmd], entry.HeldInputsOn(HostPlatform.Windows));
        Assert.Equal([Cmd, S, Middle], entry.HeldInputsOn(HostPlatform.MacOS));
        var win = entry.Holding.EditableFor(HostPlatform.MacOS)[3];
        Assert.Equal(HostPlatform.Windows, win.AuthoredOn);
        Assert.Equal("Win needs a macOS version: the Windows key has no Mac counterpart", win.NeedsOn(HostPlatform.MacOS));
    }

    [Fact]
    public void AnEditOnAPlatformMakesItsSideOwn_ThenEachPlatformChangesOnlyItsOwnSide()
    {
        var entry = WhileHolding("After Effects", Ctrl);
        var store = new MappingStore(new MappingDocument([NewGlobal()], [entry]));

        var onMac = store.AddHeldInput(entry.Id, HostPlatform.MacOS, Space);
        Assert.Equal(HostPlatform.MacOS, onMac.Holding.Own!.Platform);
        Assert.Equal([Cmd, Space], onMac.HeldInputsOn(HostPlatform.MacOS));
        Assert.Equal([Ctrl], onMac.HeldInputsOn(HostPlatform.Windows));

        var onPc = store.AddHeldInput(entry.Id, HostPlatform.Windows, S);
        Assert.Equal([Ctrl, S], onPc.HeldInputsOn(HostPlatform.Windows));
        Assert.Equal([Cmd, Space], onPc.HeldInputsOn(HostPlatform.MacOS));
    }

    [Fact]
    public void CopyFromTheOtherPlatform_ReplacesASide_FromEitherSide_AndOneUndoRestoresIt()
    {
        var entry = WhileHolding("After Effects", Ctrl);
        var store = new MappingStore(new MappingDocument([NewGlobal()], [entry]));
        store.AddHeldInput(entry.Id, HostPlatform.MacOS, Space);
        var withOwn = store.Current;

        var macCopied = store.CopyHeldInputsFromOtherPlatform(entry.Id, HostPlatform.MacOS);
        Assert.Null(macCopied.Holding.Own);
        Assert.Equal([Cmd], macCopied.HeldInputsOn(HostPlatform.MacOS));
        Assert.True(store.Undo());
        Assert.Same(withOwn, store.Current);

        var pcCopied = store.CopyHeldInputsFromOtherPlatform(entry.Id, HostPlatform.Windows);
        Assert.Null(pcCopied.Holding.Own);
        Assert.Equal(HostPlatform.MacOS, pcCopied.Holding.Origin);
        Assert.Equal([Ctrl, Space], pcCopied.HeldInputsOn(HostPlatform.Windows));
        Assert.Equal([Cmd, Space], pcCopied.HeldInputsOn(HostPlatform.MacOS));
    }

    [Fact]
    public void CopyingOneInputFromTheOtherSide_AddsItConverted_AndRefusesOneWithNoCounterpart()
    {
        var entry = WhileHolding("After Effects", Ctrl, new KeyOrButton.Modifier(KeyModifiers.Meta));
        var store = new MappingStore(new MappingDocument([NewGlobal()], [entry]));
        store.ReplaceHeldInput(entry.Id, HostPlatform.MacOS, 0, Space);
        store.RemoveHeldInput(entry.Id, HostPlatform.MacOS, 1);

        var copied = store.CopyHeldInputFromOtherPlatform(entry.Id, HostPlatform.MacOS, 0);

        Assert.Equal([Space, Cmd], copied.HeldInputsOn(HostPlatform.MacOS));
        Assert.Equal(HostPlatform.MacOS, copied.Holding.Own!.Items[1].AuthoredOn);
        var refused = Assert.Throws<MappingValidationException>(() => store.CopyHeldInputFromOtherPlatform(entry.Id, HostPlatform.MacOS, 1));
        Assert.Equal("Win needs a macOS version: the Windows key has no Mac counterpart, so it cannot be copied to macOS.", refused.Message);
    }

    [Fact]
    public void EachEditIsOneStoreChangeAndOneUndoStep()
    {
        var entry = WhileHolding("After Effects", Ctrl, S);
        var store = new MappingStore(new MappingDocument([NewGlobal()], [entry]));
        store.AddHeldInput(entry.Id, HostPlatform.MacOS, Space);
        Action[] edits =
        [
            () => store.AddHeldInput(entry.Id, HostPlatform.Windows, Middle, at: 0),
            () => store.RemoveHeldInput(entry.Id, HostPlatform.Windows, 1),
            () => store.MoveHeldInput(entry.Id, HostPlatform.Windows, 0, 1),
            () => store.ReplaceHeldInput(entry.Id, HostPlatform.Windows, 0, Alt),
            () => store.CopyHeldInputsFromOtherPlatform(entry.Id, HostPlatform.MacOS),
            () => store.CopyHeldInputFromOtherPlatform(entry.Id, HostPlatform.Windows, 2),
        ];

        foreach (var edit in edits)
        {
            var before = store.Current;
            var version = store.Version;
            edit();
            Assert.Equal(version + 1, store.Version);
            Assert.NotSame(before, store.Current);
            store.Undo();
            Assert.Same(before, store.Current);
        }
    }

    [Fact]
    public void TheRules_KeepInputsDistinctPerSide_FoldAModifierKey_AndNeverDisableAWhileHoldingEntry()
    {
        var entry = WhileHolding("After Effects", Alt, new KeyOrButton.Key(KeyCode.RightAlt), S, S, new KeyOrButton.Key(KeyCode.None)) with { DisableEntirely = true };
        var global = WhileHolding("Game", S) with { Scope = IgnoreScope.Global };

        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal()], [entry, global]));

        var stored = mapping.Ignored[0];
        Assert.False(stored.DisableEntirely);
        Assert.Equal([Alt, S], stored.Holding.Original.Select(row => row.Input));
        Assert.True(mapping.Ignored[1].Holding.IsEmpty);
        var store = new MappingStore(mapping);
        var refused = Assert.Throws<MappingValidationException>(() => store.AddHeldInput(entry.Id, HostPlatform.Windows, S));
        Assert.Equal("S is already on the Windows list of 'After Effects'.", refused.Message);
    }

    [Fact]
    public void TheHoldKeyConflict_IsFoundThroughASharedExecutableName_AndNotThroughARegexMatcher()
    {
        var navigate = HoldRemap.For(KeyCode.Space) with { Name = "Navigate" };
        var blender = NewGroup("Blender", ByProcess("blender.exe")) with { HoldRemaps = [navigate] };
        var listed = WhileHolding("Blender", Space) with { Matcher = ByProcess("BLENDER.EXE") };
        var byPattern = WhileHolding("Blender too", Space) with { Matcher = new AppMatcher { WindowsProcessNames = ["blend.*"], WindowsProcessNamesAreRegex = true } };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal(), blender], [listed, byPattern]));

        var conflict = Assert.Single(HoldKeyConflicts.On(mapping, HostPlatform.Windows));

        Assert.Equal(listed.Id, conflict.Entry.Id);
        Assert.Equal("Space is the hold key of Blender › Navigate; while Navigate runs it never ignores.", conflict.OnEntry(HostPlatform.Windows));
        Assert.Equal([conflict], HoldKeyConflicts.ForHoldRemap(mapping, blender.Id, navigate.Id, HostPlatform.Windows));
        Assert.Empty(HoldKeyConflicts.ForEntry(mapping, byPattern.Id, HostPlatform.Windows));
    }

    [Fact]
    public void TheInputsOverAWindow_AddUpAcrossMatchingEntries_AndAnotherAppHasNone()
    {
        var first = WhileHolding("After Effects", Alt, S);
        var second = WhileHolding("Effects", Middle, S) with { Matcher = ByProcess("aftereffects.exe") };
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal()], [first, second]));

        var under = IgnoreList.HeldInputsUnder(mapping, Window("aftereffects.exe"), HostPlatform.Windows);

        Assert.Equal([KeyCode.S], under.Keys);
        Assert.Equal(KeyModifiers.Alt, under.Modifiers);
        Assert.Equal(HeldButtons.Middle, under.Buttons);
        Assert.True(IgnoreList.WatchesHolding(mapping, HostPlatform.Windows));
        Assert.Same(WhileHoldingSet.None, IgnoreList.HeldInputsUnder(mapping, Window("chrome.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.Under(mapping, Window("aftereffects.exe"), HostPlatform.Windows));
    }
}
