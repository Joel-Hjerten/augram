using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Reads the synthetic full fixture (sample-config-full.json) once and checks every row of the C1 mapping table.</summary>
public sealed class FullImportTests
{
    private const string ScriptNote = "Imported from StrokesPlus.net: script-only action";
    private const string ModifierNote = "Imported from StrokesPlus.net: needs modifier/rocker support (deferred)";

    private static readonly ImportResult Result = ReadFixture();

    private static ImportResult ReadFixture()
    {
        using var stream = File.OpenRead(FixturePath.StrokesPlusNet("sample-config-full.json"));
        return StrokesPlusImporter.ReadAll(stream);
    }

    private static AppGroup Group(string name) => Result.Mapping.Groups.Single(group => group.Name == name);

    private static Command Command(string group, string name) => Group(group).Commands.Single(command => command.Name == name);

    private static GestureId GestureNamed(string name) => Result.Gestures.Single(gesture => gesture.Name == name).Id;

    private static bool HasWarning(string item, string fragment, ImportSeverity severity = ImportSeverity.Warning)
        => Result.Warnings.Any(warning => warning.Severity == severity && warning.Item == item && warning.Message.Contains(fragment, StringComparison.Ordinal));

    [Fact]
    public void GroupsAreGlobalFirstThenByName()
    {
        Assert.Equal(
            ["Global", "Synthetic Blank", "Synthetic Browser", "Synthetic Players", "Synthetic Steam Games"],
            Result.Mapping.Groups.Select(group => group.Name));
        Assert.Equal(GroupId.Global, Result.Mapping.Groups[0].Id);
        Assert.Equal(16, Group("Global").Commands.Count);
    }

    [Theory]
    [InlineData("Synthetic Close", WindowOperation.Close)]
    [InlineData("Synthetic Minimize", WindowOperation.Minimize)]
    [InlineData("Synthetic Maximize", WindowOperation.MaximizeOrRestore)]
    [InlineData("Synthetic Always On Top", WindowOperation.ToggleAlwaysOnTop)]
    [InlineData("Synthetic Center", WindowOperation.Center)]
    public void WindowMethodsBecomeWindowOpSteps(string command, WindowOperation expected)
    {
        var step = Assert.Single(Command("Global", command).Steps);

        var op = Assert.IsType<WindowOpStep>(step.Step);
        Assert.Equal(expected, op.Operation);
        Assert.Equal(HostPlatform.Windows, step.AuthoredOn);
        Assert.True(step.IsActive);
    }

    [Fact]
    public void SetWindowSizeCarriesWidthAndHeight()
    {
        var op = Assert.IsType<WindowOpStep>(Assert.Single(Command("Global", "Synthetic Set Size").Steps).Step);

        Assert.Equal(WindowOperation.SetSize, op.Operation);
        Assert.Equal(new WindowSize(1280, 720), op.Size);
    }

    [Fact]
    public void UnparsableSizeBecomesPlaceholderWithWarning()
    {
        var step = Assert.IsType<ImportedStep>(Assert.Single(Command("Global", "Synthetic Bad Size").Steps).Step);

        Assert.Equal("SetWindowSize", step.SourceMethod);
        Assert.Equal("wide", step.Parameters["width"]);
        Assert.True(HasWarning("Synthetic Bad Size", "SetWindowSize"));
    }

    [Fact]
    public void GestureTriggersResolveToTheImportedGestureIds()
    {
        var up = Trigger.ForGesture(GestureNamed("Synthetic Up"));

        Assert.Equal(up, Command("Global", "Synthetic Close").Trigger);
        Assert.Equal(up, Command("Synthetic Browser", "Synthetic Close Tab").Trigger);
        Assert.Equal(up, Command("Synthetic Players", "Synthetic Play Pause").Trigger);
    }

    [Fact]
    public void WheelActionsBecomeWheelTriggersWithMediaKeys()
    {
        var up = Command("Global", "Synthetic Volume Up");
        var down = Command("Global", "Synthetic Volume Down");

        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), up.Trigger);
        Assert.Equal(MediaKeyKind.VolumeUp, Assert.IsType<MediaKeyStep>(Assert.Single(up.Steps).Step).Key);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), down.Trigger);
        Assert.Equal(MediaKeyKind.VolumeDown, Assert.IsType<MediaKeyStep>(Assert.Single(down.Steps).Step).Key);
    }

    [Theory]
    [InlineData("Synthetic Play Pause", MediaKeyKind.PlayPause)]
    [InlineData("Synthetic Next", MediaKeyKind.NextTrack)]
    [InlineData("Synthetic Previous", MediaKeyKind.PreviousTrack)]
    [InlineData("Synthetic Stop", MediaKeyKind.Stop)]
    [InlineData("Synthetic Mute", MediaKeyKind.VolumeMute)]
    public void MediaVirtualKeysBecomeMediaKeySteps(string command, MediaKeyKind expected)
    {
        Assert.Equal(expected, Assert.IsType<MediaKeyStep>(Assert.Single(Command("Synthetic Players", command).Steps).Step).Key);
    }

    [Fact]
    public void NonMediaVirtualKeyBecomesAHotkeyWithoutModifiers()
    {
        var step = Assert.IsType<HotkeyStep>(Assert.Single(Command("Global", "Synthetic Browser Back").Steps).Step);

        Assert.Equal(KeyModifiers.None, step.Modifiers);
        Assert.Equal(KeyCode.BrowserBack, step.Key);
    }

    [Fact]
    public void SequenceKeepsOrderInactiveStepAndClampsDelay()
    {
        var steps = Command("Global", "Synthetic Alt Tab").Steps;

        Assert.Equal(5, steps.Count);
        Assert.Equal("SendAltDown", Assert.IsType<ImportedStep>(steps[0].Step).SourceMethod);
        Assert.Equal(60, Assert.IsType<DelayStep>(steps[1].Step).Milliseconds);
        Assert.False(steps[1].IsActive);
        Assert.Equal(KeyCode.Tab, Assert.IsType<HotkeyStep>(steps[2].Step).Key);
        Assert.Equal(60000, Assert.IsType<DelayStep>(steps[3].Step).Milliseconds);
        Assert.Equal("SendAltUp", Assert.IsType<ImportedStep>(steps[4].Step).SourceMethod);
        Assert.True(HasWarning("Synthetic Alt Tab", "clamped to 60000 ms"));
    }

    [Fact]
    public void ScriptOnlyActionBecomesAScriptPlaceholderWithNote()
    {
        var command = Command("Global", "Synthetic Refresh Rate");

        var step = Assert.IsType<ImportedStep>(Assert.Single(command.Steps).Step);
        Assert.Equal("Script", step.SourceMethod);
        Assert.StartsWith("sp.RunProgram", step.Parameters["script"], StringComparison.Ordinal);
        Assert.Equal(ScriptNote, command.Note);
        Assert.True(command.IsActive);
    }

    [Fact]
    public void OverrideToNothingHasNoStepsAndNoWarning()
    {
        var command = Command("Synthetic Browser", "Synthetic Nothing");

        Assert.True(command.IsOverrideToNothing);
        Assert.Null(command.Note);
        Assert.DoesNotContain(Result.Warnings, warning => warning.Item == "Synthetic Nothing");
    }

    [Fact]
    public void DanglingGestureWarnsAndImportsWithoutATrigger()
    {
        Assert.Equal(Trigger.None, Command("Global", "Synthetic Dangling").Trigger);
        Assert.True(HasWarning("Synthetic Dangling", "gesture 'Synthetic Gone' not found; command imported without a gesture"));
    }

    [Fact]
    public void ActionWithNeitherGestureNorWheelWarns()
    {
        Assert.Equal(Trigger.None, Command("Global", "Synthetic Explorer").Trigger);
        Assert.True(HasWarning("Synthetic Explorer", "without a trigger"));
    }

    [Fact]
    public void ModifierActionImportsInactiveWithNote()
    {
        var command = Command("Synthetic Browser", "Synthetic Shifted");

        Assert.False(command.IsActive);
        Assert.Equal(ModifierNote, command.Note);
        var click = Assert.IsType<ImportedStep>(Assert.Single(command.Steps).Step);
        Assert.Equal("MouseClick", click.SourceMethod);
        Assert.Equal("true", click.Parameters["down"]);
    }

    [Fact]
    public void DuplicateTriggerKeepsTheActiveCommandBound()
    {
        Assert.Equal(Trigger.None, Command("Global", "Synthetic Close Twin").Trigger);
        Assert.True(Command("Global", "Synthetic Close").Trigger.IsBound);
        Assert.True(HasWarning("Synthetic Close Twin", "already uses gesture 'Synthetic Up' in 'Global'"));
    }

    [Fact]
    public void DuplicateCommandNameGetsASuffix()
    {
        Assert.Equal(Trigger.ForGesture(GestureNamed("Synthetic Right")), Command("Global", "Synthetic Close (2)").Trigger);
        Assert.True(HasWarning("Synthetic Close", "Synthetic Close (2)"));
    }

    [Theory]
    [InlineData("Script", 1, "no scripting")]
    [InlineData("Run", 1, "Run step")]
    [InlineData("MouseClick", 1, "mouse clicks")]
    [InlineData("SendKeys", 1, "typed text")]
    [InlineData("ConsumePhysicalInput", 1, "hold-modifier")]
    [InlineData("SetWindowSize", 1, "no Augram equivalent yet")]
    public void PlaceholdersAreReportedOncePerMethod(string method, int count, string fragment)
        => AssertPlaceholderReport(method, count, fragment);

    [Theory]
    [InlineData("SendHotKey")]
    [InlineData("SendVKey")]
    public void HotkeysAndVirtualKeysAreRealStepsNotPlaceholders(string method)
        => Assert.DoesNotContain(Result.Warnings, warning => warning.Severity == ImportSeverity.Info && warning.Item == method);

    private static void AssertPlaceholderReport(string method, int count, string fragment)
    {
        var info = Assert.Single(Result.Warnings, warning => warning.Severity == ImportSeverity.Info && warning.Item == method);

        Assert.StartsWith($"{count} {method} step(s) imported as placeholders;", info.Message, StringComparison.Ordinal);
        Assert.Contains(fragment, info.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserMatcherIsAPlainProcessName()
    {
        var group = Group("Synthetic Browser");

        Assert.True(group.IsActive);
        Assert.False(group.SuppressGlobals);
        Assert.Equal(["synthetic-browser.exe"], group.Matcher!.WindowsProcessNames);
        Assert.True(group.Matcher.IgnoreWhenFullScreen);
        Assert.Null(group.Matcher.ProcessPath);
        Assert.Null(group.Matcher.Title);
        Assert.Empty(group.Matcher.ClassChain);
    }

    [Fact]
    public void PlayersMatcherSplitsTheAlternationAndBuildsTheClassChain()
    {
        var group = Group("Synthetic Players");

        Assert.True(group.SuppressGlobals);
        Assert.Equal(["alpha.exe", "beta.exe"], group.Matcher!.WindowsProcessNames);
        Assert.Equal(["Progman|WorkerW", "SHELLDLL_DefView"], group.Matcher.ClassChain);
        Assert.Equal("^Synthetic.*", group.Matcher.Title);
        Assert.True(group.Matcher.TitleIsRegex);
        Assert.True(HasWarning("Synthetic Players", "RootWindowText is used as the title; OwnerWindowText, ControlWindowText differ"));
    }

    [Fact]
    public void SteamMatcherKeepsThePathRegexAndReportsFileNameAndControlId()
    {
        var group = Group("Synthetic Steam Games");

        Assert.Equal("^C:\\\\Games\\\\.+$", group.Matcher!.ProcessPath);
        Assert.True(group.Matcher.ProcessPathIsRegex);
        Assert.Empty(group.Matcher.WindowsProcessNames);
        Assert.True(group.IsActive);
        Assert.Empty(group.Commands);
        Assert.True(HasWarning("Synthetic Steam Games", "FileName pattern 'Game.*\\.exe' is not a plain list of names"));
        Assert.True(HasWarning("Synthetic Steam Games", "ControlID"));
    }

    [Fact]
    public void EmptyMatcherImportsInactiveWithAWarning()
    {
        var group = Group("Synthetic Blank");

        Assert.False(group.IsActive);
        Assert.True(group.Matcher!.IsEmpty);
        Assert.True(HasWarning("Synthetic Blank", "needs an app definition"));
        Assert.Equal(Trigger.ForGesture(GestureNamed("Synthetic Up")), Assert.Single(group.Commands).Trigger);
    }

    [Fact]
    public void IgnoredAppsCarryModeAndActiveFlag()
    {
        var vm = Result.Mapping.Ignored.Single(app => app.Name == "Synthetic VM");
        var game = Result.Mapping.Ignored.Single(app => app.Name == "Synthetic Game Ignored");

        Assert.True(vm.DisableEntirely);
        Assert.True(vm.IsActive);
        Assert.Equal(["vmplayer.exe"], vm.Matcher.WindowsProcessNames);
        Assert.False(game.DisableEntirely);
        Assert.False(game.IsActive);
    }

    [Fact]
    public void StatsCountTheSourceAndTheResultCountsTheMapping()
    {
        Assert.Equal(new SourceStats(11, 11, 27, 4, 28, 2), Result.Stats);
        Assert.Equal(4, Result.AppGroupCount);
        Assert.Equal(27, Result.CommandCount);
        Assert.Equal(8, Result.PlaceholderStepCount); // the two SendHotKey steps and the Browser Back SendVKey are real Hotkey steps now
        Assert.Equal(2, Result.IgnoredAppCount);
    }

    [Fact]
    public void EveryGroupAndCommandIdIsFresh()
    {
        var groupIds = Result.Mapping.Groups.Select(group => group.Id).ToList();
        var commandIds = Result.Mapping.AllCommands().Select(entry => entry.Command.Id).ToList();

        Assert.Equal(groupIds.Count, groupIds.Distinct().Count());
        Assert.Equal(commandIds.Count, commandIds.Distinct().Count());
        Assert.DoesNotContain(default, commandIds);
    }

    [Fact]
    public void ReadGesturesStillReadsGesturesOnly()
    {
        using var stream = File.OpenRead(FixturePath.StrokesPlusNet("sample-config-full.json"));

        var result = StrokesPlusImporter.ReadGestures(stream);

        Assert.Equal(11, result.Gestures.Count);
        Assert.Same(MappingDocument.Empty, result.Mapping);
        Assert.Equal(0, result.CommandCount);
        Assert.DoesNotContain(result.Warnings, warning => warning.Severity == ImportSeverity.Info && warning.Item == "SendHotKey");
    }
}
