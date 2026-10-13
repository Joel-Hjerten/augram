using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Sync.Files;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// A Hotkey step's layout shortcuts in the sync (sync format 16, plan 0011): they travel inside the step, in a command item or
/// an own-steps item, so a build reading up to format 15 would read the step without them and publish the command without
/// them on every machine; the format raise pauses it instead.
/// </summary>
public sealed class SyncHotkeyLayoutsTests
{
    private static readonly StepRegistry Registry = new([HotkeyStepType.Instance]);

    [Fact]
    public void AFileWithLayoutShortcutsIsFormatSixteenOrLater_ABuildThatReadsUpToFifteenPausesOnIt()
    {
        var back = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket)
        {
            Layouts = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal) { ["com.apple.keylayout.Swedish-Pro"] = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8) },
        };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Back", Up, new CommandStep(back, HostPlatform.MacOS)))));

        var file = SyncFileSerializer.Write(new SyncFile(ConfigDocument.CurrentSchemaVersion, Guid.NewGuid(), "MAC", DateTimeOffset.UnixEpoch, [], mapping));
        var header = SyncFileSerializer.ReadHeader(file)!;
        var read = SyncFileSerializer.Read(file, Registry, notice: null);

        Assert.True(SyncFile.CurrentFormatVersion >= 16);
        Assert.True(ConfigDocument.CurrentSchemaVersion >= 8);
        Assert.Contains("\"layouts\"", file, StringComparison.Ordinal);
        Assert.True(header.IsNewerThan(schemaVersion: 7, formatVersion: 15));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 15));
        Assert.False(header.IsNewer);
        Assert.Equal(back, Assert.Single(read.Mapping.Global.Commands.Single().Steps).Step);
    }
}
