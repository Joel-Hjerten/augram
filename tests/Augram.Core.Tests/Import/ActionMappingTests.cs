using Augram.Core.Abstractions;
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
    public void SecondaryStrokeButtonWithNoneSet_ImportsAsRightPlusWheel_InactiveWithNote()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Zoom\", \"GestureName\": \"\", \"WheelUp\": true, \"UseSecondaryStrokeButton\": true, \"Steps\": [] }"));

        Assert.False(command.IsActive);
        Assert.Contains("secondary-stroke-button command", command.Note, StringComparison.Ordinal);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), command.Trigger);
    }

    [Fact]
    public void SecondaryStrokeButtonSetInTheFile_IsTheAnchor_AndTheCommandKeepsItsActiveFlag()
    {
        var result = StrokesPlusImporter.ReadAll("{ \"SecondaryStrokeButton\": 8388608, \"Gestures\": [], \"GlobalApplication\": { \"Actions\": [ "
            + "{ \"Description\": \"Synthetic Zoom\", \"GestureName\": \"\", \"WheelDown\": true, \"UseSecondaryStrokeButton\": true, \"Shift\": true, \"Capture\": 1, \"Steps\": [] } ] } }");

        var command = Only(result);
        Assert.True(command.IsActive);
        Assert.Null(command.Note);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.X1, KeyModifiers.Shift, HoldCapture.After)), command.Trigger);
    }

    [Theory]
    [InlineData(0, HoldCapture.Before)]
    [InlineData(1, HoldCapture.After)]
    [InlineData(2, HoldCapture.Either)]
    public void KeysAndButtonsImportWithTheCaptureMode_InSPNetsOrder(int capture, HoldCapture expected)
    {
        var command = Only(Read($"{{ \"Description\": \"Synthetic Chord\", \"GestureName\": \"Synthetic Up\", \"Control\": true, \"Left\": true, \"Capture\": {capture}, \"Steps\": [] }}"));

        Assert.True(command.IsActive);
        Assert.Equal(new TriggerHold(HeldButtons.Stroke | HeldButtons.Left, KeyModifiers.Control, expected), command.Trigger.Hold);
    }

    [Fact]
    public void KeysWithoutGestureOrWheel_ImportAsAClickTrigger()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Alt Click\", \"GestureName\": \"\", \"Alt\": true, \"Capture\": 0, \"Steps\": [ { \"Method\": \"CloseWindow\" } ] }"));

        Assert.True(command.IsActive);
        Assert.Equal(Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Alt, capture: HoldCapture.Before)), command.Trigger);
    }

    /// <summary>Joel, 2026-10-09: an unbound click with keys passes through, so SP.net's "Shift+Right Click" workaround is not needed.</summary>
    [Fact]
    public void ShiftRightClickWorkaround_ImportsInactive_BecauseAugramRelaysTheClickItself()
    {
        var command = Only(Read("{ \"Description\": \"Shift+Right Click\", \"GestureName\": \"\", \"Shift\": true, \"Capture\": 0, \"Steps\": [ "
            + "{ \"Method\": \"ConsumePhysicalInput\", \"MethodParameters\": [ { \"Name\": \"active\", \"Value\": true } ] }, "
            + "{ \"Method\": \"MouseClick\", \"MethodParameters\": [ { \"Name\": \"button\", \"Value\": \"Right\" } ] } ] }"));

        Assert.False(command.IsActive);
        Assert.Contains("passes a click with keys held through", command.Note, StringComparison.Ordinal);
        Assert.IsType<Trigger.ClickTrigger>(command.Trigger);
    }

    [Fact]
    public void ACombinationAndThePlainTriggerAreBothBound_ButTwoOverlappingCombinationsAreNot()
    {
        var result = Read(
            "{ \"Description\": \"Synthetic Plain\", \"GestureName\": \"Synthetic Up\", \"Steps\": [] }, "
            + "{ \"Description\": \"Synthetic Shift Either\", \"GestureName\": \"Synthetic Up\", \"Shift\": true, \"Capture\": 2, \"Steps\": [] }, "
            + "{ \"Description\": \"Synthetic Shift Before\", \"GestureName\": \"Synthetic Up\", \"Shift\": true, \"Capture\": 0, \"Steps\": [] }");

        var commands = result.Mapping.Global.Commands;
        Assert.True(commands.Single(command => command.Name == "Synthetic Plain").Trigger.IsBound);
        Assert.True(commands.Single(command => command.Name == "Synthetic Shift Either").Trigger.IsBound);
        Assert.False(commands.Single(command => command.Name == "Synthetic Shift Before").Trigger.IsBound);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Shift Before" && warning.Message.Contains("Shift + gesture 'Synthetic Up'", StringComparison.Ordinal));
    }

    [Fact]
    public void ScriptBesideStepsIsIgnored()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Mixed\", \"GestureName\": \"Synthetic Up\", \"Steps\": [ { \"Method\": \"CloseWindow\" } ], \"Script\": \"sp.Beep();\" }"));

        Assert.IsType<WindowOpStep>(Assert.Single(command.Steps).Step);
        Assert.Null(command.Note);
    }

    [Fact]
    public void ScriptOnlyActionWithAKey_KeepsTheKeyAndTheScriptNote()
    {
        var command = Only(Read("{ \"Description\": \"Synthetic Scripted\", \"GestureName\": \"Synthetic Up\", \"Alt\": true, \"Steps\": [], \"Script\": \"sp.Beep();\" }"));

        Assert.True(command.IsActive);
        Assert.Equal(KeyModifiers.Alt, command.Trigger.Hold.Keys);
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
