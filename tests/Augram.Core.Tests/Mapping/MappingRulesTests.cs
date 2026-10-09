using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

public sealed class MappingRulesTests
{
    [Fact]
    public void ValidDocumentSortsGlobalFirstThenGroupsAndCommandsByNameIgnoringCase()
    {
        var document = Document(
            NewGroup("zeta", commands: [NewCommand("b"), NewCommand("A"), NewCommand("c")]),
            NewGroup("Alpha"),
            NewGlobal(NewCommand("Minimize"), NewCommand("close")));

        var valid = MappingRules.ValidDocument(document);

        Assert.Equal(["Global", "Alpha", "zeta"], valid.Groups.Select(group => group.Name));
        Assert.Equal(["close", "Minimize"], valid.Global.Commands.Select(command => command.Name));
        Assert.Equal(["A", "b", "c"], valid.Groups[2].Commands.Select(command => command.Name));
        Assert.Same(valid.Groups[0], valid.Global);
    }

    [Fact]
    public void NamesAreTrimmedAndEmptyNamesAreRejected()
    {
        var trimmed = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("  Close  ")), NewGroup("  Chrome ")));
        Assert.Equal("Chrome", trimmed.Groups[1].Name);
        Assert.Equal("Close", trimmed.Global.Commands[0].Name);

        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("   "))));
        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(NewCommand("")))));
        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            new MappingDocument([NewGlobal()], [new IgnoredApp(GroupId.New(), " ", true, ByProcess("x.exe"), false)])));
    }

    [Theory]
    [InlineData("Chrome")]
    [InlineData("chrome")]
    [InlineData(" CHROME ")]
    public void GroupNamesMustBeUniqueIgnoringCase(string duplicate)
    {
        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Chrome"), NewGroup(duplicate))));

        Assert.Contains("'Chrome' already exists", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandNamesMustBeUniqueWithinAGroupButMayRepeatAcrossGroups()
    {
        MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Close")), NewGroup("Chrome", commands: [NewCommand("close")])));

        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal(), NewGroup("Chrome", commands: [NewCommand("Close"), NewCommand("close ")]))));

        Assert.Contains("'Close' already exists in 'Chrome'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATriggerIsBoundAtMostOncePerGroup()
    {
        var wheelUp = Trigger.ForWheel(WheelDirection.Up);
        MappingRules.ValidDocument(Document(
            NewGlobal(NewCommand("Close", Up), NewCommand("Volume", wheelUp), NewCommand("Later"), NewCommand("Later too")),
            NewGroup("Chrome", commands: [NewCommand("Close tab", Up), NewCommand("Zoom", wheelUp)])));

        var gesture = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal(), NewGroup("Chrome", commands: [NewCommand("Close tab", Up), NewCommand("Other", Up)]))));
        Assert.Equal("'Close tab' in 'Chrome' already uses this gesture.", gesture.Message);

        var wheel = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal(NewCommand("Volume", wheelUp), NewCommand("Zoom", wheelUp)))));
        Assert.Equal("'Volume' in 'Global' already uses wheel up.", wheel.Message);
    }

    [Fact]
    public void ExactlyOneGlobalGroupIsRequiredAndItIsMovedFirst()
    {
        var missing = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGroup("Chrome"))));
        Assert.Equal("The Global group is missing.", missing.Message);

        var twice = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), NewGlobal() with { Name = "Second" })));
        Assert.Equal("Only one Global group is allowed.", twice.Message);

        var reordered = MappingRules.ValidDocument(Document(NewGroup("Alpha"), NewGroup("Beta"), NewGlobal()));
        Assert.True(reordered.Groups[0].IsGlobal);
    }

    [Fact]
    public void TheGlobalGroupNeverHasAMatcherOrSuppressesItself()
    {
        var odd = NewGlobal() with { Matcher = ByProcess("x.exe"), SuppressGlobals = true };

        var valid = MappingRules.ValidDocument(Document(odd));

        Assert.Null(valid.Global.Matcher);
        Assert.False(valid.Global.SuppressGlobals);
    }

    [Fact]
    public void AnInvalidPatternInAGroupOrIgnoredAppIsRejected()
    {
        var bad = new AppMatcher { ProcessPath = "[", ProcessPathIsRegex = true };

        var group = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("X", bad))));
        Assert.Contains("path pattern '['", group.Message, StringComparison.Ordinal);

        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            new MappingDocument([NewGlobal()], [new IgnoredApp(GroupId.New(), "X", true, bad, false)])));
    }

    /// <summary>Every field with a regex toggle is checked (2026-10-09): the executable names one by one, and each per-window field.</summary>
    [Theory]
    [InlineData("executable name pattern '['")]
    [InlineData("macOS executable name pattern '['")]
    [InlineData("root title pattern '['")]
    [InlineData("control class pattern '['")]
    public void AnInvalidPatternInAnyRegexFieldIsRejected(string expected)
    {
        var bad = expected switch
        {
            "executable name pattern '['" => new AppMatcher { WindowsProcessNames = ["ok.exe", "["], WindowsProcessNamesAreRegex = true },
            "macOS executable name pattern '['" => new AppMatcher { MacProcessNames = ["["], MacProcessNamesAreRegex = true },
            "root title pattern '['" => new AppMatcher { RootTitle = "[", RootTitleIsRegex = true },
            _ => new AppMatcher { ControlClass = "[", ControlClassIsRegex = true },
        };

        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("X", bad))));
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        MappingRules.EnsureValid(new AppMatcher { WindowsProcessNames = ["["], RootTitle = "[" });
    }

    [Fact]
    public void IdsAreUniqueAcrossGroupsCommandsAndIgnoredApps()
    {
        var command = NewCommand("Same");
        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal(command), NewGroup("Chrome", commands: [command with { Name = "Other" }]))));
        Assert.Contains("more than one group", ex.Message, StringComparison.Ordinal);

        var group = NewGroup("Chrome");
        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(NewGlobal(), group, group with { Name = "Other" })));

        var ignored = new IgnoredApp(GroupId.New(), "Game", true, ByProcess("game.exe"), false);
        Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            new MappingDocument([NewGlobal()], [ignored, ignored with { Name = "Other" }])));
    }

    [Fact]
    public void StepListsMayBeEmptyAndATriggerlessCommandIsAllowed()
    {
        var valid = MappingRules.ValidDocument(Document(NewGlobal(), NewGroup("Steam", commands: [NewCommand("Close", Up)])));

        var close = Assert.Single(valid.Groups[1].Commands);
        Assert.True(close.IsOverrideToNothing);
        Assert.False(NewCommand("Later").Trigger.IsBound);
    }
}
