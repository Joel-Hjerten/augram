using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Sync;

public sealed class SyncItemTests
{
    public static TheoryData<SyncItemKey> Keys =>
    [
        SyncItemKey.ForGesture(GestureId.New()),
        SyncItemKey.ForGroup(GroupId.New()),
        SyncItemKey.ForCategory(GroupId.Global, CategoryId.New()),
        SyncItemKey.ForCommand(CommandId.New()),
        SyncItemKey.ForIgnored(GroupId.New()),
    ];

    [Theory]
    [MemberData(nameof(Keys))]
    public void AKeyRoundTripsThroughItsText(SyncItemKey key)
    {
        Assert.True(SyncItemKey.TryParse(key.ToString(), out var back));

        Assert.Equal(key, back);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gesture")]
    [InlineData("thing:6d1e7f3a-0000-4000-8000-000000000001")]
    [InlineData("command:nope")]
    [InlineData("category:6d1e7f3a-0000-4000-8000-000000000001")]
    public void TextThatIsNotAKeyIsRefused(string? text) => Assert.False(SyncItemKey.TryParse(text, out _));

    [Fact]
    public void ADocumentSplitsIntoOneItemPerGestureGroupCategoryCommandAndIgnoredApp()
    {
        var (gestures, mapping) = SyncSamples.Setup();

        var items = SyncItemSet.From(gestures, mapping);

        Assert.Equal((3, 2, 1, 3, 1), (
            items.CountOf(SyncItemKind.Gesture),
            items.CountOf(SyncItemKind.Group),
            items.CountOf(SyncItemKind.Category),
            items.CountOf(SyncItemKind.Command),
            items.CountOf(SyncItemKind.Ignored)));
        var group = items.OfType<SyncItem.GroupItem>().Single(item => item.Name == "Chrome");
        Assert.DoesNotContain("Close tab", group.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryItemsContentReadsBackToTheSameItem()
    {
        var (gestures, mapping) = SyncSamples.Setup();

        foreach (var item in SyncItemSet.From(gestures, mapping))
        {
            var back = SyncItem.Parse(item.Key, item.Content, FakeStepType.Registry);

            Assert.Equal(item.Key, back.Key);
            Assert.Equal(item.Content, back.Content);
            Assert.Equal(item.Name, back.Name);
        }
    }

    [Fact]
    public void ACommandsContentCarriesItsGroupSoAMoveIsAChange()
    {
        var command = MappingFixtures.NewCommand("Close");

        var inGlobal = new SyncItem.CommandItem(GroupId.Global, command);
        var elsewhere = new SyncItem.CommandItem(GroupId.New(), command);

        Assert.Equal(inGlobal.Key, elsewhere.Key);
        Assert.NotEqual(inGlobal.Content, elsewhere.Content);
    }
}
