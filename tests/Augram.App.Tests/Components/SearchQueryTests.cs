using Augram.App.Components.SearchBox;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The one search rule (plan 0009 decision 2): words all match, case-insensitive, an empty query matches everything, starts-with for the picker's order.</summary>
public sealed class SearchQueryTests
{
    [Fact]
    public void EveryWordMustBeSomewhereInTheName_CaseInsensitive()
    {
        var query = SearchQuery.Parse("down left");

        Assert.False(query.IsEmpty);
        Assert.True(query.Matches("Down then left"));
        Assert.True(query.Matches("LEFT and down"));
        Assert.False(query.Matches("Down then right"));
        Assert.False(query.Matches(null));
        Assert.Equal("down left", query.Text);
    }

    [Fact]
    public void AnEmptyOrBlankQueryMatchesEverything()
    {
        Assert.True(SearchQuery.Parse(null).IsEmpty);
        Assert.True(SearchQuery.Parse("   ").IsEmpty);
        Assert.True(SearchQuery.Parse("  ").Matches("anything"));
        Assert.True(SearchQuery.Parse(string.Empty).Matches(null));
        Assert.False(SearchQuery.Parse(string.Empty).StartsWith("anything"));
    }

    [Fact]
    public void TwoTextsMatchWhenEveryWordIsInOneOfThem()
    {
        var query = SearchQuery.Parse("blender off");

        Assert.True(query.Matches("Blender", "Gestures off over this app · blender.exe"));
        Assert.False(query.Matches("Blender", "Disable while focused · blender.exe"));
    }

    [Fact]
    public void StartsWithIsTheWholeTypedText_AndTheEmptyStateQuotesIt()
    {
        var query = SearchQuery.Parse(" do ");

        Assert.True(query.StartsWith("Down"));
        Assert.True(query.StartsWith("down then left"));
        Assert.False(query.StartsWith("Up then down"));
        Assert.Equal("No gestures match 'do'", query.NoneMatch("gestures"));
    }
}
