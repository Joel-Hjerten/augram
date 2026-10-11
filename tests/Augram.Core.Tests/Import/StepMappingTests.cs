using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Inline documents for the step rows the full fixture does not cover: odd values, clamping, case, skipped entries.</summary>
public sealed class StepMappingTests
{
    private static ImportResult Read(string stepsJson)
        => StrokesPlusImporter.ReadAll(
            "{ \"GlobalApplication\": { \"Actions\": [ { \"Description\": \"Synthetic Probe\", \"GestureName\": \"\", \"WheelUp\": true, \"Steps\": [ " + stepsJson + " ] } ] } }");

    private static Command Probe(ImportResult result) => Assert.Single(result.Mapping.Global.Commands);

    private static CommandStep OnlyStep(string stepJson) => Assert.Single(Probe(Read(stepJson)).Steps);

    [Fact]
    public void NullMethodBecomesAPlaceholderReportedAsNoMethod()
    {
        var result = Read("{ \"Method\": null, \"MethodParameters\": [ { \"Name\": \"virtualKey\", \"Value\": null } ] }");

        var step = Assert.IsType<ImportedStep>(Assert.Single(Probe(result).Steps).Step);
        Assert.Equal(string.Empty, step.SourceMethod);
        Assert.Equal("(no method)", step.Description);
        Assert.Equal(string.Empty, step.Parameters["virtualKey"]);
        Assert.Contains(result.Warnings, warning => warning.Severity == ImportSeverity.Info && warning.Item == "(no method)");
    }

    [Fact]
    public void BoolAndObjectParameterValuesBecomeText()
    {
        var consume = Assert.IsType<ImportedStep>(OnlyStep("{ \"Method\": \"ConsumePhysicalInput\", \"MethodParameters\": [ { \"Name\": \"active\", \"Value\": true } ] }").Step);
        var click = Assert.IsType<ImportedStep>(OnlyStep("{ \"Method\": \"MouseClick\", \"MethodParameters\": [ { \"Name\": \"point\", \"Value\": { \"X\": 3, \"Y\": 4 } } ] }").Step);

        Assert.Equal("true", consume.Parameters["active"]);
        Assert.Equal("{\"X\":3,\"Y\":4}", click.Parameters["point"]);
    }

    [Fact]
    public void SendHotKeyBecomesAHotkeyStep()
    {
        var hotkey = Assert.IsType<HotkeyStep>(OnlyStep("{ \"Method\": \"SendHotKey\", \"MethodParameters\": [ { \"Name\": \"hotkey\", \"Value\": { \"LControl\": true, \"Key\": 9 } } ] }").Step);

        Assert.Equal(KeyModifiers.Control, hotkey.Modifiers);
        Assert.Equal(KeyCode.Tab, hotkey.Key);
        Assert.Equal(KeyModifiers.None, hotkey.RightHand);
    }

    [Fact]
    public void SendHotKeyKeepsARightHandModifier()
    {
        var hotkey = Assert.IsType<HotkeyStep>(OnlyStep("{ \"Method\": \"SendHotKey\", \"MethodParameters\": [ { \"Name\": \"hotkey\", \"Value\": { \"LControl\": false, \"RAlt\": true, \"Key\": 120 } } ] }").Step);

        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), hotkey);
        Assert.Equal("RAlt+F9", hotkey.Summary);
    }

    [Fact]
    public void NegativeDelayClampsToZeroWithWarning()
    {
        var result = Read("{ \"Method\": \"Delay\", \"MethodParameters\": [ { \"Name\": \"milliseconds\", \"Value\": -5 } ] }");

        Assert.Equal(0, Assert.IsType<DelayStep>(Assert.Single(Probe(result).Steps).Step).Milliseconds);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Probe" && warning.Message.Contains("clamped to 0 ms", StringComparison.Ordinal));
    }

    [Fact]
    public void LongDelayClampsToTheStepsMaximumWithWarning()
    {
        var result = Read("{ \"Method\": \"Delay\", \"MethodParameters\": [ { \"Name\": \"milliseconds\", \"Value\": 90000 } ] }");

        Assert.Equal(60_000, Assert.IsType<DelayStep>(Assert.Single(Probe(result).Steps).Step).Milliseconds);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Probe" && warning.Message == "Delay of 90000 ms clamped to 60000 ms.");
    }

    [Fact]
    public void FractionalDelayRounds()
    {
        Assert.Equal(60, Assert.IsType<DelayStep>(OnlyStep("{ \"Method\": \"Delay\", \"MethodParameters\": [ { \"Name\": \"milliseconds\", \"Value\": 59.6 } ] }").Step).Milliseconds);
    }

    [Fact]
    public void UnparsableDelayBecomesAPlaceholderWithWarning()
    {
        var result = Read("{ \"Method\": \"Delay\", \"MethodParameters\": [ { \"Name\": \"milliseconds\", \"Value\": \"soon\" } ] }");

        var step = Assert.IsType<ImportedStep>(Assert.Single(Probe(result).Steps).Step);
        Assert.Equal("Delay", step.SourceMethod);
        Assert.Equal("soon", step.Parameters["milliseconds"]);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Probe" && warning.Message.Contains("Delay has no usable", StringComparison.Ordinal));
    }

    [Fact]
    public void InvokedMethodOtherThanCenterIsAPlaceholder()
    {
        var step = Assert.IsType<ImportedStep>(OnlyStep("{ \"Method\": \"InvokeObjectMethodByName\", \"MethodParameters\": [ { \"Name\": \"methodName\", \"Value\": \"Maximize\" } ] }").Step);

        Assert.Equal("InvokeObjectMethodByName", step.SourceMethod);
        Assert.Equal("Maximize", step.Parameters["methodName"]);
    }

    [Fact]
    public void CenterIsMatchedCaseInsensitively()
    {
        var op = Assert.IsType<WindowOpStep>(OnlyStep("{ \"Method\": \"InvokeObjectMethodByName\", \"MethodParameters\": [ { \"Name\": \"methodName\", \"Value\": \"center\" } ] }").Step);

        Assert.Equal(WindowOperation.Center, op.Operation);
    }

    [Theory]
    [InlineData(173, MediaKeyKind.VolumeMute)]
    [InlineData(174, MediaKeyKind.VolumeDown)]
    [InlineData(175, MediaKeyKind.VolumeUp)]
    [InlineData(176, MediaKeyKind.NextTrack)]
    [InlineData(177, MediaKeyKind.PreviousTrack)]
    [InlineData(178, MediaKeyKind.Stop)]
    [InlineData(179, MediaKeyKind.PlayPause)]
    public void EveryMediaVirtualKeyMaps(int virtualKey, MediaKeyKind expected)
    {
        var step = OnlyStep("{ \"Method\": \"SendVKey\", \"MethodParameters\": [ { \"Name\": \"virtualKey\", \"Value\": " + virtualKey + " } ] }");

        Assert.Equal(expected, Assert.IsType<MediaKeyStep>(step.Step).Key);
    }

    [Theory]
    [InlineData(172, KeyCode.BrowserHome)]
    [InlineData(9, KeyCode.Tab)]
    public void OtherVirtualKeysBecomeHotkeysWithoutModifiers(int virtualKey, KeyCode expected)
    {
        var step = OnlyStep("{ \"Method\": \"SendVKey\", \"MethodParameters\": [ { \"Name\": \"virtualKey\", \"Value\": " + virtualKey + " } ] }");

        var hotkey = Assert.IsType<HotkeyStep>(step.Step);
        Assert.Equal(KeyModifiers.None, hotkey.Modifiers);
        Assert.Equal(expected, hotkey.Key);
    }

    [Fact]
    public void AVirtualKeyWithNoAugramKeyStaysAPlaceholder()
    {
        var step = OnlyStep("{ \"Method\": \"SendVKey\", \"MethodParameters\": [ { \"Name\": \"virtualKey\", \"Value\": 7 } ] }");

        Assert.Equal("SendVKey", Assert.IsType<ImportedStep>(step.Step).SourceMethod);
    }

    [Fact]
    public void NonObjectStepIsSkippedWithWarning()
    {
        var result = Read("5, { \"Method\": \"CloseWindow\" }");

        Assert.Single(Probe(result).Steps);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Probe" && warning.Message.Contains("Step 1 is not an object", StringComparison.Ordinal));
    }

    [Fact]
    public void InactiveStepStaysInactive()
    {
        var step = OnlyStep("{ \"Method\": \"CloseWindow\", \"Active\": false }");

        Assert.False(step.IsActive);
        Assert.IsType<WindowOpStep>(step.Step);
    }

    [Fact]
    public void MissingParametersGiveTheSharedEmptySet()
    {
        var step = Assert.IsType<ImportedStep>(OnlyStep("{ \"Method\": \"SendAltDown\" }").Step);

        Assert.Same(ImportedStep.NoParameters, step.Parameters);
        Assert.Equal("SendAltDown", step.Description);
    }

    [Fact]
    public void ASendKeysStringThatMakesNoStepsStaysAPlaceholderWithoutAWarning()
    {
        var result = Read("{ \"Method\": \"SendKeys\", \"MethodParameters\": [ { \"Name\": \"sendKeysString\", \"Value\": \"{LEFT 0}\" } ] }");

        Assert.Equal("SendKeys", Assert.IsType<ImportedStep>(Assert.Single(Probe(result).Steps).Step).SourceMethod);
        Assert.DoesNotContain(result.Warnings, warning => warning.Message.StartsWith("SendKeys:", StringComparison.Ordinal));
    }

    [Fact]
    public void StepsAreAuthoredOnWindows()
    {
        Assert.Equal(HostPlatform.Windows, OnlyStep("{ \"Method\": \"CloseWindow\" }").AuthoredOn);
    }
}
