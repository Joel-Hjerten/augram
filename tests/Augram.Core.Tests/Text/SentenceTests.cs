using Augram.Core.Sync;
using Augram.Core.Text;
using Xunit;

namespace Augram.Core.Tests.Text;

/// <summary>The words every line is made of (Core/Text): counts, lists and a capital first letter, and the sync kinds' nouns that count through them.</summary>
public sealed class SentenceTests
{
    [Fact]
    public void ACountNamesItsNoun()
    {
        Assert.Equal("1 command", Sentence.Count(1, "command"));
        Assert.Equal("3 commands", Sentence.Count(3, "command"));
        Assert.Equal("0 commands", Sentence.Count(0, "command"));
        Assert.Equal("no commands", Sentence.Count(0, "command", zeroAsNo: true));
        Assert.Equal("2 categories", Sentence.Count(2, "category", "categories"));
        Assert.Equal("1200 samples", Sentence.Count(1200, "sample"));
    }

    [Fact]
    public void AListIsJoinedWithAnAndBeforeTheLast()
    {
        Assert.Equal(string.Empty, Sentence.List([]));
        Assert.Equal("a", Sentence.List(["a"]));
        Assert.Equal("a and b", Sentence.List(["a", "b"]));
        Assert.Equal("a, b and c", Sentence.List(["a", "b", "c"]));
    }

    [Fact]
    public void OnlyTheFirstLetterIsCapitalised()
    {
        Assert.Equal("Scroll up", Sentence.Capitalised("scroll up"));
        Assert.Equal("Step 2 of 'Orbit'", Sentence.Capitalised("step 2 of 'Orbit'"));
        Assert.Equal(string.Empty, Sentence.Capitalised(string.Empty));
    }

    [Fact]
    public void TheSyncKindsCountWithTheirOwnNouns()
    {
        Assert.Equal("2 app groups", SyncItemNouns.Count(SyncItemKind.Group, 2));
        Assert.Equal("1 command's own steps", SyncItemNouns.Count(SyncItemKind.CommandVersion, 1));
        Assert.Equal("Own steps", SyncItemNouns.Heading(SyncItemKind.CommandVersion));
        Assert.Equal("groups", SyncItemNouns.LogPlural(SyncItemKind.Group));
        Assert.Equal(Enum.GetValues<SyncItemKind>().Length, SyncItemNouns.ReadingOrder.Distinct().Count());
    }

    [Fact]
    public void ThePhraseNamesTheKindsThatMovedInReadingOrder()
    {
        var counts = new SyncCounts(new(1, 0, 0), new(2, 0, 0), SyncKindCounts.None, new(0, 8, 0), SyncKindCounts.None) { HoldRemaps = new(1, 0, 0) };

        Assert.Equal("1 gesture, 2 app groups and 1 hold remap", counts.Phrase(kind => kind.Added));
        Assert.Equal("8 commands", counts.Phrase(kind => kind.Changed));
        Assert.Equal(string.Empty, counts.Phrase(kind => kind.Deleted));
    }
}
