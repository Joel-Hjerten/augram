using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Unknown;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

public sealed class MappingSerializationTests
{
    private readonly List<string> _notices = [];

    [Fact]
    public void RoundTripPreservesGroupsCommandsTriggersStepsAndOverrides()
    {
        var document = new ConfigDocument { Mapping = FullMapping() };

        var json = ConfigSerializer.Write(document);
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add);

        Assert.Empty(_notices);
        AssertSameMapping(document.Mapping, back.Mapping);
        Assert.Equal(json, ConfigSerializer.Write(back));
    }

    [Fact]
    public void FileShapeFollowsTheReadme()
    {
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = FullMapping() }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\"mapping\": {\n    \"groups\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"matcher\": null", json, StringComparison.Ordinal);
        Assert.Contains($"\"gesture\": \"{Up.Value}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"wheel\": \"Up\"", json, StringComparison.Ordinal);
        Assert.Contains("\"wheel\": \"Down\"", json, StringComparison.Ordinal);
        Assert.Contains("\"trigger\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"fake\"", json, StringComparison.Ordinal);
        Assert.Contains("\"authoredOn\": \"Windows\"", json, StringComparison.Ordinal);
        Assert.Contains("\"authoredOn\": \"MacOS\"", json, StringComparison.Ordinal);
        Assert.Contains("\"params\": {\n                  \"text\": \"min\"\n                }", json, StringComparison.Ordinal);
        Assert.Contains("\"ownVersion\": {\n              \"platform\": \"MacOS\",", json, StringComparison.Ordinal);
        Assert.Contains("\"changedAt\": \"2026-10-07T12:00:00.000Z\"", json, StringComparison.Ordinal);
        Assert.Contains("\"note\": \"Imported from StrokesPlus.net: script-only action\\nsp.Foo();\"", json, StringComparison.Ordinal);
        Assert.Contains("\"processNames\": [\n            \"chrome.exe\",\n            \"msedge.exe\"\n          ]", json, StringComparison.Ordinal);
        Assert.Contains("\"ignoreWhenFullScreen\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"suppressGlobals\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"disableEntirely\": true", json, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(json, "\"ownVersion\""));
        Assert.Equal(0, Occurrences(json, "\"overrides\""));
        Assert.Equal(1, Occurrences(json, "\"note\""));
    }

    /// <summary>The app definition's fields (schema 3) are written only when set, each toggle only when on.</summary>
    [Fact]
    public void MatcherFieldsAreWrittenOnlyWhenSet()
    {
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = FullMapping() }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\"processNamesAreRegex\": true", json, StringComparison.Ordinal);
        Assert.Matches("\"parentTitle\": \"\\^\\$\",\\s+\"parentTitleIsRegex\": true", json);
        Assert.Matches("\"rootClass\": \"Progman\",\\s+\"parentClass\"", json);
        Assert.Equal(1, Occurrences(json, "\"rootTitle\""));
        Assert.Equal(1, Occurrences(json, "\"processNamesAreRegex\""));
        Assert.Equal(0, Occurrences(json, "\"macProcessNamesAreRegex\""));
        Assert.Equal(0, Occurrences(json, "\"rootClassIsRegex\""));
    }

    [Fact]
    public void AnUnknownStepTypeIsKeptAsIsWithANotice_AndSavesBackUnchanged()
    {
        var mapping = FullMapping();
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping });

        var back = ConfigSerializer.Read(json, new StepRegistry([]), _notices.Add);

        Assert.Equal(4, back.Mapping.Groups.Count);
        Assert.Equal(3, back.Mapping.Global.Commands.Count);
        Assert.All(back.Mapping.AllCommands().SelectMany(pair => pair.Command.Steps), step => Assert.Equal("fake", Assert.IsType<UnknownStep>(step.Step).StoredTypeKey));
        Assert.Equal(5, _notices.Count);
        Assert.Contains("Step 1 of the own version of command 'Minimize' in 'Global' kept as is: unknown step type 'fake'.", _notices);
        Assert.Contains("Step 1 of command 'Minimize' in 'Global' kept as is: unknown step type 'fake'.", _notices);
        Assert.Contains("Step 2 of command 'Minimize' in 'Global' kept as is: unknown step type 'fake'.", _notices);
        Assert.Contains("Step 1 of command 'Type' in 'Chrome' kept as is: unknown step type 'fake'.", _notices);

        // The older build saves: the file is what the newer one wrote, and the newer one reads its steps back.
        var saved = ConfigSerializer.Write(back);
        Assert.Equal(json, saved);
        AssertSameMapping(mapping, ConfigSerializer.Read(saved, FakeStepType.Registry, notice: null).Mapping);
    }

    [Fact]
    public void AStepItsTypeRefusesIsKeptAsIs_AndAStepLevelOverridesMemberIsPassedOver()
    {
        const string json = """
            { "schemaVersion": 1, "mapping": { "groups": [ { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", "commands": [
              { "id": "6d1e7f3a-0000-4000-8000-000000000001", "name": "Mixed", "steps": [
                { "type": "fake", "params": { "text": 5 } },
                { "type": "fake", "params": { "text": "ok" }, "overrides": { "macOS": { "text": [] }, "linux": { "text": "x" } } },
                { "type": "bogus", "params": {} }
              ] } ] } ] } }
            """;

        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add);

        var command = Assert.Single(back.Mapping.Global.Commands);
        Assert.Equal(3, command.Steps.Count);
        var refused = Assert.IsType<UnknownStep>(command.Steps[0].Step);
        Assert.Equal(("fake", "{\"text\":5}", "'text' must be a string"), (refused.StoredTypeKey, refused.ParametersJson, refused.Reason));
        var unknown = Assert.IsType<UnknownStep>(command.Steps[2].Step);
        Assert.Equal(("bogus", "{}"), (unknown.StoredTypeKey, unknown.ParametersJson));
        var step = command.Steps[1];
        Assert.Equal("ok", ((FakeStep)step.Step).Text);
        Assert.Equal(HostPlatform.Windows, step.AuthoredOn);
        Assert.True(step.IsActive);
        Assert.Null(command.OwnVersion);
        Assert.Equal(2, _notices.Count);
        Assert.Contains("Step 1 of command 'Mixed' in 'Global' kept as is: 'text' must be a string.", _notices);
        Assert.Contains("Step 3 of command 'Mixed' in 'Global' kept as is: unknown step type 'bogus'.", _notices);
    }

    [Fact]
    public void AMissingMappingReadsAsEmptyAndTheDefaultDocumentHasAnEmptyMapping()
    {
        Assert.Same(MappingDocument.Empty, ConfigDocument.Default.Mapping);
        Assert.Same(MappingDocument.Empty, new ConfigDocument().Mapping);
        Assert.Same(MappingDocument.Empty, ConfigSerializer.Read("{ \"schemaVersion\": 1 }").Mapping);
        Assert.Same(MappingDocument.Empty, ConfigSerializer.Read("{ \"schemaVersion\": 1, \"mapping\": null }").Mapping);

        var noGroups = ConfigSerializer.Read("{ \"schemaVersion\": 1, \"mapping\": { } }").Mapping;
        Assert.True(Assert.Single(noGroups.Groups).IsGlobal);
        Assert.Empty(noGroups.Ignored);
    }

    [Fact]
    public void AHandWrittenMappingTakesDefaultsForEveryOptionalMember()
    {
        const string json = """
            { "schemaVersion": 1, "mapping": { "groups": [
              { "id": "00000000-0000-4000-8000-000000000001", "name": "Global" },
              { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Chrome", "matcher": { "processNames": ["chrome.exe"] },
                "commands": [ { "id": "6d1e7f3a-0000-4000-8000-000000000003", "name": "Zoom", "trigger": { "wheel": "down" } },
                              { "id": "6d1e7f3a-0000-4000-8000-000000000004", "name": "Later" } ] } ],
              "ignored": [ { "id": "6d1e7f3a-0000-4000-8000-000000000005", "name": "Game" } ] } }
            """;

        var back = ConfigSerializer.Read(json);

        Assert.Null(back.Mapping.Global.Matcher);
        var chrome = back.Mapping.Groups[1];
        Assert.True(chrome.IsActive);
        Assert.False(chrome.SuppressGlobals);
        Assert.Equal(["chrome.exe"], chrome.Matcher!.WindowsProcessNames);
        Assert.Null(chrome.Matcher.Title);
        Assert.Empty(chrome.Matcher.ClassChain);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), chrome.Commands[0].Trigger);
        Assert.Same(Trigger.None, chrome.Commands[1].Trigger);
        Assert.Empty(chrome.Commands[0].Steps);
        Assert.Null(chrome.Commands[0].Note);
        Assert.True(chrome.Commands[0].IsActive);
        var game = Assert.Single(back.Mapping.Ignored);
        Assert.True(game.IsActive);
        Assert.False(game.DisableEntirely);
        Assert.True(game.Matcher.IsEmpty);
        Assert.Equal(2, new MappingStore(back.Mapping).Current.Groups.Count);
    }

    [Theory]
    [InlineData("\"mapping\": []", "'mapping' must be a JSON object")]
    [InlineData("\"mapping\": { \"groups\": [ { \"name\": \"Global\" } ] }", "'id' of app group 'Global' is required")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"x\", \"name\": \"Global\" } ] }", "'id' of app group 'Global' must be a Guid string")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"isActive\": \"yes\" } ] }", "must be true or false")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"commands\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\" } ] } ] }", "'name' of a command in 'Global' is required")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"commands\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\", \"name\": \"X\", \"trigger\": { \"rocker\": true } } ] } ] }", "'trigger' of command 'X' in 'Global' must be null")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"commands\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\", \"name\": \"X\", \"trigger\": { \"wheel\": \"Sideways\" } } ] } ] }", "must be one of Up, Down")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"commands\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\", \"name\": \"X\", \"steps\": [ { \"params\": {} } ] } ] } ] }", "'type' of step 1 of command 'X' in 'Global' is required")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"commands\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\", \"name\": \"X\", \"steps\": [ { \"type\": \"fake\", \"params\": 3 } ] } ] } ] }", "'params' of step 1 of command 'X' in 'Global' must be a JSON object")]
    [InlineData("\"mapping\": { \"groups\": [ { \"id\": \"00000000-0000-4000-8000-000000000001\", \"name\": \"Global\", \"matcher\": { \"processNames\": \"chrome.exe\" } } ] }", "'processNames' of 'matcher' of app group 'Global' must be an array")]
    [InlineData("\"mapping\": { \"ignored\": [ { \"id\": \"00000000-0000-4000-8000-000000000002\" } ] }", "'name' of an ignored app is required")]
    public void StructuralProblemsAreFormatErrorsThatNameTheMember(string mapping, string messagePart)
    {
        var ex = Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Read($"{{ \"schemaVersion\": 1, {mapping} }}", FakeStepType.Registry, _notices.Add));

        Assert.Contains(messagePart, ex.Message, StringComparison.Ordinal);
        Assert.Empty(_notices);
    }

    [Fact]
    public void FileConfigStoreReadsWithTheRegistryItWasGiven()
    {
        using var folder = new TempFolder();
        var store = new FileConfigStore(folder.Path, _notices.Add, steps: FakeStepType.Registry);
        store.Save(new ConfigDocument { Mapping = FullMapping() });

        var loaded = store.Load();

        Assert.Empty(_notices);
        var minimize = loaded.Mapping.Global.Commands.Single(command => command.Name == "Minimize");
        Assert.Equal("min", ((FakeStep)minimize.Steps[0].Step).Text);
        Assert.Equal("mac-min", ((FakeStep)minimize.StepsFor(HostPlatform.MacOS)[0].Step).Text);
        Assert.False(minimize.IsOwnVersionStale);

        var builtIn = new FileConfigStore(folder.Path, _notices.Add).Load();
        Assert.All(builtIn.Mapping.Global.Commands.Single(command => command.Name == "Minimize").Steps, step => Assert.IsType<UnknownStep>(step.Step));
        Assert.Equal(5, _notices.Count);
    }

    /// <summary>Every trigger kind, a note, a command with its own macOS steps, an inactive step, every matcher field, both ignore modes.</summary>
    private static MappingDocument FullMapping()
    {
        var minimize = new CommandStep(new FakeStep("min"), HostPlatform.Windows);
        var typed = new CommandStep(new FakeStep("hello"), HostPlatform.MacOS, IsActive: false);
        var minimizeCommand = NewCommand("Minimize", Up, minimize, NewStep("second"))
            .WithStepsFor(HostPlatform.MacOS, [new CommandStep(new FakeStep("mac-min"), HostPlatform.MacOS)], new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
        return MappingRules.ValidDocument(new MappingDocument(
        [
            NewGlobal(
                minimizeCommand,
                NewCommand("Volume up", Trigger.ForWheel(WheelDirection.Up), NewStep("vol")),
                NewCommand("Later") with { Note = "Imported from StrokesPlus.net: script-only action\nsp.Foo();" }),
            NewGroup("Chrome", new AppMatcher { WindowsProcessNames = ["chrome.exe", "msedge.exe"], Title = "^.*Google.*$", TitleIsRegex = true },
                NewCommand("Type", Trigger.ForWheel(WheelDirection.Down), typed)),
            NewGroup("Steam", new AppMatcher { ProcessPath = @"^C:\\Steam\\.+$", ProcessPathIsRegex = true, IgnoreWhenFullScreen = true },
                NewCommand("Close", Up)),
            NewGroup("Desktop", new AppMatcher
            {
                ClassChain = ["Progman|WorkerW", "SysListView32"],
                WindowsProcessNames = [@"PotPlayerMini.*\.exe"],
                WindowsProcessNamesAreRegex = true,
                MacProcessNames = ["Finder"],
                MacTitle = "Desktop",
                MacProcessPath = "/System/Library/CoreServices/Finder.app/Contents/MacOS/Finder",
                RootTitle = "Program Manager",
                ParentTitle = "^$",
                ParentTitleIsRegex = true,
                ControlTitle = "FolderView",
                OwnerClass = "^(Progman|WorkerW)$",
                OwnerClassIsRegex = true,
                RootClass = "Progman",
                ParentClass = "SHELLDLL_DefView",
                ControlClass = "SysListView32",
                ControlClassIsRegex = true,
            }) with { SuppressGlobals = true, IsActive = false },
        ],
        [
            new IgnoredApp(GroupId.New(), "Game", IsActive: true, ByProcess("game.exe"), DisableEntirely: true),
            new IgnoredApp(GroupId.New(), "VM", IsActive: false, new AppMatcher { Title = "VMware" }, DisableEntirely: false),
        ]));
    }

    private static void AssertSameMapping(MappingDocument expected, MappingDocument actual)
    {
        Assert.Equal(expected.Groups.Count, actual.Groups.Count);
        foreach (var (group, back) in expected.Groups.Zip(actual.Groups))
        {
            Assert.Equal(group.Id, back.Id);
            Assert.Equal(group.Name, back.Name);
            Assert.Equal(group.IsActive, back.IsActive);
            Assert.Equal(group.SuppressGlobals, back.SuppressGlobals);
            Assert.Equal(group.Matcher, back.Matcher, MatcherComparer.Instance);
            Assert.Equal(group.Commands.Count, back.Commands.Count);
            foreach (var (command, backCommand) in group.Commands.Zip(back.Commands))
            {
                Assert.Equal(command.Id, backCommand.Id);
                Assert.Equal(command.Name, backCommand.Name);
                Assert.Equal(command.Trigger, backCommand.Trigger);
                Assert.Equal(command.IsActive, backCommand.IsActive);
                Assert.Equal(command.Note, backCommand.Note);
                Assert.Equal(command.Steps, backCommand.Steps);
            }
        }

        Assert.Equal(expected.Ignored.Count, actual.Ignored.Count);
        foreach (var (app, back) in expected.Ignored.Zip(actual.Ignored))
        {
            Assert.Equal(app.Id, back.Id);
            Assert.Equal(app.Name, back.Name);
            Assert.Equal(app.IsActive, back.IsActive);
            Assert.Equal(app.DisableEntirely, back.DisableEntirely);
            Assert.Equal(app.Matcher, back.Matcher, MatcherComparer.Instance);
        }
    }

    private static int Occurrences(string text, string part)
        => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;

    /// <summary>Field-by-field, because the record compares its two lists by reference.</summary>
    private sealed class MatcherComparer : IEqualityComparer<AppMatcher?>
    {
        public static MatcherComparer Instance { get; } = new();

        public bool Equals(AppMatcher? x, AppMatcher? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            // Every scalar member through the record's own equality, with the lists compared by content.
            return x.WindowsProcessNames.SequenceEqual(y.WindowsProcessNames, StringComparer.Ordinal)
                && x.MacProcessNames.SequenceEqual(y.MacProcessNames, StringComparer.Ordinal)
                && x.ClassChain.SequenceEqual(y.ClassChain, StringComparer.Ordinal)
                && x with { WindowsProcessNames = [], MacProcessNames = [], ClassChain = [] } == y with { WindowsProcessNames = [], MacProcessNames = [], ClassChain = [] };
        }

        public int GetHashCode(AppMatcher? obj) => 0;
    }
}
