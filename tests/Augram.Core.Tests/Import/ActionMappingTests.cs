using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.WindowOp;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Inline documents for the action rows the full fixture does not cover: wheel edge cases, naming, notes, trigger clashes, lookups.</summary>
public sealed class ActionMappingTests
{
    private const string UpGesture = "{ \"Name\": \"Synthetic Up\", \"PointPatterns\": [ { \"Points\": [ { \"X\": 0, \"Y\": 100 }, { \"X\": 0, \"Y\": 0 } ] } ] }";

    private static ImportResult Read(string actionsJson, string gesturesJson = UpGesture)
        => StrokesPlusImporter.ReadAll("{ \"Gestures\": [ " + gesturesJson + " ], \"GlobalApplication\": { \"Actions\": [ " + actionsJson + " ] } }");

    private static Command Only(ImportResult result) => Assert.Single(result.Mapping.Global.Commands);

    [Fact]
    public void BothWheelDirectionsImportAsWheelUpWithWarning()
    {
        var result = Read("{ \"Description\": \"Synthetic Both\", \"GestureName\": \"\", \"WheelUp\": true, \"WheelDown\": true, \"Steps\": [] }");

        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), Only(result).Trigger);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Both" && warning.Message.Contains("Both wheel directions", StringComparison.Ordinal));
    }

    [Fact]
    public void NonObjectActionEntryIsSkippedWithWarning()
    {
        var result = Read("\"text\", { \"Description\": \"Synthetic Kept\", \"GestureName\": \"Synthetic Up\", \"Steps\": [] }");

        Assert.Equal("Synthetic Kept", Only(result).Name);
        Assert.Contains(result.Warnings, warning => warning.Item == "Global #1" && warning.Message.Contains("not an object", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingDescriptionGetsANumberedName()
    {
        Assert.Equal("Action 1", Only(Read("{ \"GestureName\": \"Synthetic Up\", \"Steps\": [] }")).Name);
    }

    [Fact]
    public void SecondaryStrokeButtonImportsInactiveWithNote()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Zoom\", \"GestureName\": \"\", \"WheelUp\": true, \"UseSecondaryStrokeButton\": true, \"Steps\": [] }"));

        Assert.False(command.IsActive);
        Assert.Contains("modifier/rocker", command.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptBesideStepsIsIgnored()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Mixed\", \"GestureName\": \"Synthetic Up\", \"Steps\": [ { \"Method\": \"CloseWindow\" } ], \"Script\": \"sp.Beep();\" }"));

        Assert.IsType<WindowOpStep>(Assert.Single(command.Steps).Step);
        Assert.Null(command.Note);
    }

    [Fact]
    public void ScriptAndModifierNotesAreBothKept()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Scripted\", \"GestureName\": \"Synthetic Up\", \"Alt\": true, \"Steps\": [], \"Script\": \"sp.Beep();\" }"));

        Assert.False(command.IsActive);
        Assert.Contains("modifier/rocker", command.Note, StringComparison.Ordinal);
        Assert.Contains("script-only", command.Note, StringComparison.Ordinal);
        Assert.Single(command.Steps);
    }

    [Fact]
    public void ActiveFlagIsCarried()
    {
        Assert.False(Only(Read("{ \"Description\": \"Synthetic Off\", \"Active\": false, \"GestureName\": \"Synthetic Up\", \"Steps\": [] }")).IsActive);
    }

    [Fact]
    public void ActiveActionTakesTheTriggerFromAnEarlierInactiveOne()
    {
        var result = Read(
            "{ \"Description\": \"Synthetic Sleeper\", \"Active\": false, \"GestureName\": \"Synthetic Up\", \"Steps\": [] }, "
            + "{ \"Description\": \"Synthetic Awake\", \"Active\": true, \"GestureName\": \"Synthetic Up\", \"Steps\": [] }");

        var commands = result.Mapping.Global.Commands;
        Assert.True(commands.Single(command => command.Name == "Synthetic Awake").Trigger.IsBound);
        Assert.Equal(Trigger.None, commands.Single(command => command.Name == "Synthetic Sleeper").Trigger);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Sleeper" && warning.Message.Contains("both use gesture 'Synthetic Up'", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoActiveActionsOnOneTriggerKeepTheFirstBound()
    {
        var result = Read(
            "{ \"Description\": \"Synthetic First\", \"GestureName\": \"\", \"WheelDown\": true, \"Steps\": [] }, "
            + "{ \"Description\": \"Synthetic Second\", \"GestureName\": \"\", \"WheelDown\": true, \"Steps\": [] }");

        var commands = result.Mapping.Global.Commands;
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), commands.Single(command => command.Name == "Synthetic First").Trigger);
        Assert.Equal(Trigger.None, commands.Single(command => command.Name == "Synthetic Second").Trigger);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Second" && warning.Message.Contains("already uses wheel down", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingGlobalApplicationGivesAnEmptyGlobalAndInfo()
    {
        var result = StrokesPlusImporter.ReadAll("{ \"Gestures\": [] }");

        Assert.Empty(result.Mapping.Global.Commands);
        Assert.Single(result.Mapping.Groups);
        Assert.Contains(result.Warnings, warning => warning.Severity == ImportSeverity.Info && warning.Item == "GlobalApplication");
    }

    [Fact]
    public void GestureNameMatchesCaseInsensitively()
    {
        var result = Read("{ \"Description\": \"Synthetic Lower\", \"GestureName\": \"synthetic up\", \"Steps\": [] }");

        Assert.Equal(Trigger.ForGesture(Assert.Single(result.Gestures).Id), Only(result).Trigger);
    }

    [Fact]
    public void GestureSkippedForLackOfSamplesIsDangling()
    {
        var result = Read(
            "{ \"Description\": \"Synthetic Orphan\", \"GestureName\": \"Synthetic Empty\", \"Steps\": [] }",
            gesturesJson: "{ \"Name\": \"Synthetic Empty\", \"PointPatterns\": null }");

        Assert.Empty(result.Gestures);
        Assert.Equal(Trigger.None, Only(result).Trigger);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Orphan" && warning.Message.Contains("not found", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateSourceGestureNameBindsToTheFirst()
    {
        var result = Read(
            "{ \"Description\": \"Synthetic Twin User\", \"GestureName\": \"Synthetic Up\", \"Steps\": [] }",
            gesturesJson: UpGesture + ", " + UpGesture);

        Assert.Equal(["Synthetic Up", "Synthetic Up (2)"], result.Gestures.Select(gesture => gesture.Name));
        Assert.Equal(Trigger.ForGesture(result.Gestures[0].Id), Only(result).Trigger);
    }
}
