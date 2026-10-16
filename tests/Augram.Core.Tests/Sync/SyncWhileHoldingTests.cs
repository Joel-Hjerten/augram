using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Sync;
using Augram.Core.Sync.Files;
using Augram.Core.Tests.Config;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// Exclusions › While holding entries in the sync (sync format 17, plan 0012): an entry travels as one ignored-app item with its
/// inputs on both sides, so a build reading up to format 16 would publish it without them; the format raise pauses it instead.
/// </summary>
public sealed class SyncWhileHoldingTests
{
    private static readonly StepRegistry Registry = new([HotkeyStepType.Instance]);

    [Fact]
    public void AWhileHoldingEntryTravelsAsOneItemWithBothSides_AndABuildThatReadsUpToSixteenPausesOnIt()
    {
        var mapping = MappingRules.ValidDocument(new MappingDocument([NewGlobal()], [WhileHoldingSerializationTests.AfterEffects()]));

        var file = SyncFileSerializer.Write(new SyncFile(ConfigDocument.CurrentSchemaVersion, Guid.NewGuid(), "PC", DateTimeOffset.UnixEpoch, [], mapping));
        var header = SyncFileSerializer.ReadHeader(file)!;
        var read = SyncFileSerializer.Read(file, Registry, notice: null);
        var item = Assert.Single(SyncItemSet.From([], mapping).OfType<SyncItem.IgnoredItem>());

        Assert.True(SyncFile.CurrentFormatVersion >= 17);
        Assert.True(header.IsNewerThan(schemaVersion: 8, formatVersion: 16));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 16));
        Assert.False(header.IsNewer);
        Assert.Contains("\"ownHolding\"", item.Content, StringComparison.Ordinal);
        Assert.Equal(item.Content, SyncItem.Parse(item.Key, item.Content, Registry).Content);
        Assert.Equal(item.Content, new SyncItem.IgnoredItem(Assert.Single(read.Mapping.Ignored)).Content);
    }
}
