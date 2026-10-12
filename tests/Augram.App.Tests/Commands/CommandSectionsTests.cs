using Augram.App.Components.CommandTree;
using Augram.App.Components.SearchBox;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The tree rule of the search (plan 0009 decision 4) in <see cref="CommandSections.For"/>, after the platform filter.</summary>
public sealed class CommandSectionsTests
{
    private static readonly GestureLibrary Library = new(StarterGestures.All());

    [Fact]
    public void ASectionMatchingByNameShowsAllItsCommandsExpanded()
    {
        var sections = For(CommandsScope.Apps, "chro");

        var chrome = Assert.Single(sections);
        Assert.Equal("Chrome", chrome.Name);
        Assert.True(chrome.IsExpanded);
        Assert.Equal(["Close tab", "Nothing on Up"], chrome.Commands.Select(command => command.Name));
    }

    [Fact]
    public void ACommandMatchShowsOnlyItUnderItsExpandedSection()
    {
        var sections = For(CommandsScope.Global, "close");

        var window = Assert.Single(sections);
        Assert.Equal("Window", window.Name);
        Assert.True(window.IsExpanded);
        Assert.Equal("Close window", Assert.Single(window.Commands).Name);
    }

    [Fact]
    public void OnTheAppsTabACategoryNameMatchesToo_ShowingTheCommandsTaggedWithIt()
    {
        var sections = For(CommandsScope.Apps, "general");

        var photoshop = Assert.Single(sections);
        Assert.Equal("Photoshop", photoshop.Name);
        Assert.True(photoshop.IsExpanded);
        Assert.Equal("Brush", Assert.Single(photoshop.Commands).Name);
    }

    [Fact]
    public void ThePlatformFilterStillAppliesFirst()
    {
        var store = Store();
        store.UpdateGroup(Group(store, "Chrome") with { UseOn = PlatformSet.Windows });

        Assert.Empty(CommandSections.For(CommandsScope.Apps, store.Current, new HashSet<SectionId>(), Library.Find, HostPlatform.MacOS, search: SearchQuery.Parse("chrome")));
        Assert.Equal("Chrome", Assert.Single(CommandSections.For(CommandsScope.Apps, store.Current, new HashSet<SectionId>(), Library.Find, HostPlatform.MacOS, showOtherPlatforms: true, search: SearchQuery.Parse("chrome"))).Name);
    }

    [Fact]
    public void AnEmptyQueryLeavesTheSavedExpandedSetInCharge()
    {
        var store = Store();
        var chrome = SectionId.ForGroup(Group(store, "Chrome").Id);

        var sections = CommandSections.For(CommandsScope.Apps, store.Current, new HashSet<SectionId> { chrome }, Library.Find, HostPlatform.Windows, search: SearchQuery.Parse("  "));

        Assert.Equal(["Apple", "Chrome", "Photoshop"], sections.Select(section => section.Name));
        Assert.Equal([false, true, false], sections.Select(section => section.IsExpanded));
    }

    [Fact]
    public void NothingMatchingLeavesNoSection()
    {
        Assert.Empty(For(CommandsScope.Apps, "zzz"));
    }

    private static IReadOnlyList<SectionItem> For(CommandsScope scope, string text)
        => CommandSections.For(scope, Store().Current, new HashSet<SectionId>(), Library.Find, HostPlatform.Windows, search: SearchQuery.Parse(text));
}
