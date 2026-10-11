using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Run;
using Augram.Core.Steps.TypeText;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// Export (plan 0003): each scope written and read back as a file, the config file's format with members left out; never the
/// sync or appearance section; a selection carries its groups whole and the gestures their commands use; the names and counts the dialog shows.
/// </summary>
public sealed class ExportTests
{
    [Fact]
    public void Everything_RoundTripsTheWholeConfiguration_AndItsOptionsWithoutTheSyncAndAppearanceSections()
    {
        var config = SampleConfig();

        var json = TransferSerializer.Write(Exporter.Export(ExportScope.Everything, config));
        var back = TransferSerializer.Read(json, Registry);

        Assert.Equal(Contents(config), Contents(back.Gestures, back.Mapping));
        Assert.Equal(config.Settings with { Sync = SyncSettings.Default, Appearance = AppearanceSettings.Default }, back.Settings);
        Assert.Equal(["schemaVersion", "settings", "gestures", "mapping"], Members(json));
        Assert.DoesNotContain("\"sync\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"appearance\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(RepositoryUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain(config.Settings.Sync.MachineId.ToString(), json, StringComparison.Ordinal);
        Assert.Equal(json, TransferSerializer.Write(back));
    }

    [Fact]
    public void GesturesOnly_WritesTheLibraryAlone_CleanedShapesWithTheirOriginals()
    {
        var config = SampleConfig();
        var left = GestureNamed(config.Gestures, "Left");
        config = config with { Gestures = [.. config.Gestures.Select(gesture => gesture == left ? gesture with { OriginalSamples = [new GestureSample([new(0, 0), new(5, 1), new(10, 0)])] } : gesture)] };

        var json = TransferSerializer.Write(Exporter.Export(ExportScope.GesturesOnly, config));
        var back = TransferSerializer.Read(json, Registry);

        Assert.Equal(["schemaVersion", "gestures"], Members(json));
        Assert.Null(back.Mapping);
        Assert.Null(back.Settings);
        Assert.Equal(Contents(config.Gestures, null), Contents(back.Gestures, null));
        Assert.NotNull(GestureNamed(back.Gestures, "Left").OriginalSamples);
    }

    [Fact]
    public void ASelectedGroup_TravelsWhole_WithTheGesturesItsCommandsUse_AndAGlobalShell()
    {
        var config = SampleConfig();
        var chrome = GroupNamed(config.Mapping, "Chrome");

        var back = Exported(config, ExportScope.Of([chrome.Id]));

        Assert.Equal(["Down"], back.Gestures.Select(gesture => gesture.Name));
        Assert.Null(back.Settings);
        var mapping = Assert.IsType<MappingDocument>(back.Mapping);
        Assert.Equal([GroupId.Global, chrome.Id], mapping.Groups.Select(group => group.Id));
        Assert.True(TransferContents.IsShell(mapping.Global));
        Assert.Empty(mapping.Ignored);
        var shell = config.Mapping.Global with { Commands = [], Categories = [] };
        Assert.Equal(Contents([GestureNamed(config.Gestures, "Down")], new MappingDocument([shell, chrome], [])), Contents(back.Gestures, mapping));
    }

    [Fact]
    public void ASelection_CarriesTheGestureOfAnOwnVersionsTrigger_InLibraryOrder()
    {
        var config = SampleConfig();
        var left = GestureNamed(config.Gestures, "Left");
        var back = NewCommand("Back", Trigger.None, NewStep("alt+left")) with
        {
            OwnVersion = new CommandVersion(HostPlatform.MacOS, [NewStep("cmd+[", HostPlatform.MacOS)], "0", DateTimeOffset.UnixEpoch) { Trigger = Trigger.ForGesture(left.Id) },
        };
        config = WithGroup(config, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands, back] });

        var file = Exported(config, ExportScope.Of([GroupNamed(config.Mapping, "Chrome").Id]));

        Assert.Equal(["Down", "Left"], file.Gestures.Select(gesture => gesture.Name));
        Assert.Equal(Trigger.ForGesture(left.Id), CommandNamed(file.Mapping!, "Chrome", "Back").OwnVersion!.Trigger);
    }

    [Fact]
    public void Global_IsExportableAsAGroup_WithItsCategoriesAndCommands()
    {
        var config = SampleConfig();

        var file = Exported(config, ExportScope.Of([GroupId.Global]));

        var global = file.Mapping!.Global;
        Assert.Equal(["Window"], global.Categories.Select(category => category.Name));
        Assert.Equal(["Close", "Minimize"], global.Commands.Select(command => command.Name));
        Assert.Equal(global.Categories[0].Id, CommandNamed(file.Mapping, AppGroup.GlobalName, "Minimize").CategoryId);
        Assert.Equal(["Up", "Down"], file.Gestures.Select(gesture => gesture.Name));
        Assert.True(TransferContents.Of(file).HasGlobal);
    }

    [Fact]
    public void HoldRemapsTravelWithTheirGroup_AndIgnoredAppsAreSelectable()
    {
        var config = SampleConfig();
        var blender = GroupNamed(config.Mapping, "Blender");
        var game = config.Mapping.Ignored.Single();

        var file = Exported(config, ExportScope.Of([blender.Id], [game.Id]));

        var back = GroupNamed(file.Mapping!, "Blender");
        Assert.Equal(blender.HoldRemaps, back.HoldRemaps);
        Assert.All(back.Commands, command => Assert.Equal(blender.HoldRemaps[0].Id, command.HoldRemapId));
        Assert.Equal(blender.Commands.Count, back.Commands.Count);
        Assert.Equal(new SyncItem.IgnoredItem(game).Content, new SyncItem.IgnoredItem(file.Mapping!.Ignored.Single()).Content);
        Assert.Empty(file.Gestures);
    }

    [Fact]
    public void AnExportedSelection_IsAValidConfigFile()
    {
        var config = SampleConfig();
        var json = TransferSerializer.Write(Exporter.Export(ExportScope.Of([GroupNamed(config.Mapping, "Chrome").Id]), config));

        var asConfig = ConfigSerializer.Read(json, Registry, notice: null);

        Assert.Equal(Settings.Default, asConfig.Settings);
        Assert.Equal([AppGroup.GlobalName, "Chrome"], MappingRules.ValidDocument(asConfig.Mapping).Groups.Select(group => group.Name));
    }

    [Theory]
    [InlineData("everything", "Augram everything 2026-10-10.augram.json")]
    [InlineData("gestures", "Augram gestures 2026-10-10.augram.json")]
    [InlineData("Chrome", "Augram Chrome 2026-10-10.augram.json")]
    [InlineData("Chrome+Blender", "Augram 2 groups 2026-10-10.augram.json")]
    [InlineData("Game", "Augram Game 2026-10-10.augram.json")]
    [InlineData("Chrome+Game", "Augram selection 2026-10-10.augram.json")]
    [InlineData("Odd", "Augram A-B- C- 2026-10-10.augram.json")]
    public void SuggestedFileNames(string what, string expected)
    {
        var config = SampleConfig();
        var odd = NewGroup("A/B: C?", ByProcess("odd.exe"));
        var mapping = MappingRules.ValidDocument(config.Mapping with { Groups = [.. config.Mapping.Groups, odd] });
        var game = mapping.Ignored.Single().Id;
        ExportScope scope = what switch
        {
            "everything" => ExportScope.Everything,
            "gestures" => ExportScope.GesturesOnly,
            "Odd" => ExportScope.Of([odd.Id]),
            "Game" => ExportScope.Of([], [game]),
            "Chrome+Game" => ExportScope.Of([GroupNamed(mapping, "Chrome").Id], [game]),
            _ => ExportScope.Of(what.Split('+').Select(name => GroupNamed(mapping, name).Id)),
        };

        Assert.Equal(expected, Exporter.SuggestedFileName(scope, mapping, new DateOnly(2026, 10, 10)));
    }

    [Fact]
    public void TheContentsLine_CountsWhatTheFileHolds()
    {
        var contents = TransferContents.Of(Exporter.Export(ExportScope.Everything, SampleConfig()));

        Assert.Equal(new TransferContents(3, 2, HasGlobal: true, 1, 1, 13, 1, HasSettings: true, PrivateTextSteps: 0), contents);
        Assert.Equal("options, 3 gestures, Global, 2 app groups, 1 hold remap, 13 commands, 1 excluded app", contents.ToString());
        Assert.Equal("1 gesture", TransferContents.Of(new TransferFile([Flick("Up")], null)).ToString());
    }

    [Fact]
    public void StepsThatMayHoldPrivateText_AreCounted_OwnVersionsIncluded()
    {
        var config = SampleConfig();
        var login = NewCommand("Log in", Trigger.None, new CommandStep(new TypeTextStep("hunter2"), HostPlatform.Windows), new CommandStep(new RunStep("cmd.exe", "/c token"), HostPlatform.Windows), NewStep("enter")) with
        {
            OwnVersion = new CommandVersion(HostPlatform.MacOS, [new CommandStep(new TypeTextStep("hunter2"), HostPlatform.MacOS)], "0", DateTimeOffset.UnixEpoch),
        };
        config = WithGroup(config, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands, login] });

        var file = Exported(config, ExportScope.Of([GroupNamed(config.Mapping, "Chrome").Id]));

        Assert.Equal(3, TransferContents.Of(file).PrivateTextSteps);
        Assert.Equal(0, TransferContents.Of(Exporter.Export(ExportScope.GesturesOnly, config)).PrivateTextSteps);
    }

    private static string[] Members(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(member => member.Name)];
    }
}
