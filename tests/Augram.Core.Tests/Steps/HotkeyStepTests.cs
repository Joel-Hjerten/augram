using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class HotkeyStepTests
{
    private static readonly HotkeyStepType Type = HotkeyStepType.Instance;

    [Fact]
    public void MetadataAndTheUnsetDefault()
    {
        Assert.Equal("hotkey", Type.Key);
        Assert.Equal("Hotkey", Type.DisplayName);
        Assert.Equal(StepCategory.Keyboard, Type.Category);
        Assert.False(Type.IsPlatformNeutral);

        var step = Assert.IsType<HotkeyStep>(Type.CreateDefault());
        Assert.Equal(HotkeyStep.Unset, step);
        Assert.False(step.IsSet);
        Assert.Equal(KeyModifiers.None, step.Modifiers);
        Assert.Equal(KeyCode.None, step.Key);
        Assert.Equal("Hotkey (no key set)", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T, "Ctrl+Shift+T", """{"modifiers":"Control, Shift","key":"T"}""")]
    [InlineData(KeyModifiers.Alt, KeyCode.F4, "Alt+F4", """{"modifiers":"Alt","key":"F4"}""")]
    [InlineData(KeyModifiers.None, KeyCode.Escape, "Esc", """{"modifiers":"None","key":"Escape"}""")]
    [InlineData(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit5, "Alt+Win+5", """{"modifiers":"Alt, Meta","key":"Digit5"}""")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, KeyCode.PageUp, "Ctrl+Alt+Shift+Win+PgUp", """{"modifiers":"Control, Alt, Shift, Meta","key":"PageUp"}""")]
    [InlineData(KeyModifiers.Shift, KeyCode.None, "Hotkey (no key set)", """{"modifiers":"Shift","key":"None"}""")]
    public void SummaryAndAByteStableRoundTrip(KeyModifiers modifiers, KeyCode key, string expectedSummary, string expectedJson)
    {
        var step = new HotkeyStep(modifiers, key);

        Assert.Equal(expectedSummary, step.Summary);
        var written = Type.Write(step);
        Assert.Equal(expectedJson, written.ToJsonString());
        var reread = Type.Read(written);
        Assert.Equal(step, reread);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Fact]
    public void ReadDefaultsMissingMembersAndMatchesNamesIgnoringCase()
    {
        Assert.Equal(HotkeyStep.Unset, Type.Read(StepJson.Object("{}")));
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.W), Type.Read(StepJson.Object("""{ "key": "w" }""")));
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.None), Type.Read(StepJson.Object("""{ "modifiers": "control" }""")));
        Assert.Equal(
            new HotkeyStep(KeyModifiers.Control | KeyModifiers.Meta, KeyCode.Left),
            Type.Read(StepJson.Object("""{ "modifiers": " meta ,CONTROL, ", "key": "left" }""")));
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.Tab), Type.Read(StepJson.Object("""{ "modifiers": "", "key": "Tab" }""")));
        Assert.Equal(new HotkeyStep(KeyModifiers.Shift, KeyCode.Tab), Type.Read(StepJson.Object("""{ "modifiers": "None, Shift", "key": "Tab" }""")));
    }

    [Theory]
    [InlineData(KeyModifiers.Alt, KeyModifiers.Alt, KeyCode.F9, "RAlt+F9", """{"modifiers":"Alt","rightHand":"Alt","key":"F9"}""")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Control | KeyModifiers.Shift, KeyCode.P, "RCtrl+RShift+P", """{"modifiers":"Control, Shift","rightHand":"Control, Shift","key":"P"}""")]
    [InlineData(KeyModifiers.Control, KeyModifiers.Control, KeyCode.Digit0, "RCtrl+0", """{"modifiers":"Control","rightHand":"Control","key":"Digit0"}""")]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt, KeyModifiers.Alt, KeyCode.F10, "Ctrl+RAlt+F10", """{"modifiers":"Control, Alt","rightHand":"Alt","key":"F10"}""")]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Meta, KeyModifiers.Meta, KeyCode.S, "Shift+RWin+S", """{"modifiers":"Shift, Meta","rightHand":"Meta","key":"S"}""")]
    public void RightHandModifiersRoundTripByteStable(KeyModifiers modifiers, KeyModifiers rightHand, KeyCode key, string expectedSummary, string expectedJson)
    {
        var step = new HotkeyStep(modifiers, key, rightHand);

        Assert.Equal(expectedSummary, step.Summary);
        var written = Type.Write(step);
        Assert.Equal(expectedJson, written.ToJsonString());
        var reread = Type.Read(written);
        Assert.Equal(step, reread);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Fact]
    public void AFileWithoutRightHandReadsAsPlainModifiers()
    {
        var step = Assert.IsType<HotkeyStep>(Type.Read(StepJson.Object("""{ "modifiers": "Alt", "key": "F9" }""")));

        Assert.Equal(KeyModifiers.None, step.RightHand);
        Assert.Equal("Alt+F9", step.Summary);
        Assert.False(Type.Write(step).ContainsKey(HotkeyStepType.RightHandMember));
    }

    [Fact]
    public void RightHandIsCutDownToTheModifiersOnReadAndWrite()
    {
        Assert.Equal(
            new HotkeyStep(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt),
            Type.Read(StepJson.Object("""{ "modifiers": "Alt", "rightHand": "control, ALT", "key": "F9" }""")));
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9), Type.Read(StepJson.Object("""{ "modifiers": "Alt", "rightHand": "Shift", "key": "F9" }""")));
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9), Type.Read(StepJson.Object("""{ "modifiers": "Alt", "rightHand": "None", "key": "F9" }""")));

        var stray = new HotkeyStep(KeyModifiers.Control, KeyCode.T, KeyModifiers.Alt | KeyModifiers.Control);
        Assert.Equal("""{"modifiers":"Control","rightHand":"Control","key":"T"}""", Type.Write(stray).ToJsonString());
        Assert.Equal("RCtrl+T", stray.Summary);
        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.T, KeyModifiers.Control), stray.Normalized());
    }

    [Fact]
    public void NormalizedReturnsTheSameInstanceWhenThereIsNothingToCut()
    {
        var step = new HotkeyStep(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt);

        Assert.Same(step, step.Normalized());
        Assert.Same(HotkeyStep.Unset, HotkeyStep.Unset.Normalized());
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.F9), new HotkeyStep(KeyModifiers.None, KeyCode.F9, KeyModifiers.Alt).Normalized());
    }

    [Theory]
    [InlineData("""{ "modifiers": "Ctrl", "key": "T" }""", "'modifiers' must list Control, Alt, Shift, Meta separated by commas, or None; got 'Ctrl'.")]
    [InlineData("""{ "modifiers": "Control, 2", "key": "T" }""", "'modifiers' must list Control, Alt, Shift, Meta separated by commas, or None; got 'Control, 2'.")]
    [InlineData("""{ "modifiers": 3, "key": "T" }""", "'modifiers' must be a string.")]
    [InlineData("""{ "modifiers": "Alt", "rightHand": "RAlt", "key": "F9" }""", "'rightHand' must list Control, Alt, Shift, Meta separated by commas, or None; got 'RAlt'.")]
    [InlineData("""{ "modifiers": "Alt", "rightHand": true, "key": "F9" }""", "'rightHand' must be a string.")]
    [InlineData("""{ "modifiers": "Control", "key": 20 }""", "'key' must be a string.")]
    [InlineData("""{ "modifiers": "Control", "key": "20" }""", null)]
    [InlineData("""{ "modifiers": "Control", "key": "Hyper" }""", null)]
    public void BadInputNamesTheMember(string json, string? expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        if (expectedMessage is null)
        {
            Assert.StartsWith("'key' must be one of None, A, B,", ex.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedMessage, ex.Message);
        }
    }

    [Fact]
    public void WriteAndExecuteRefuseAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
        Assert.Throws<ArgumentException>(() => Type.Execute(new DelayStep(1), StepContexts.Create()));
    }

    [Fact]
    public void ExecuteSendsTheModifiersAndKeyAsOneHotkey()
    {
        var input = new FakeInputSimulator();
        var log = new CountingEventLog();

        var result = Type.Execute(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T), StepContexts.Create(input: input, log: log));

        Assert.Equal(StepResult.Done, result);
        Assert.Equal(["hotkey Control, Shift+T"], input.Calls);
        var line = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Hotkey"), (line.Level, line.Source, line.Message));
        Assert.Contains(new LogProperty("keys", "Ctrl+Shift+T"), line.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), line.Properties!);
    }

    [Fact]
    public void ExecutePassesTheRightHandSetThrough_CutDownToTheModifiers()
    {
        var input = new FakeInputSimulator();
        var log = new CountingEventLog();

        var result = Type.Execute(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), StepContexts.Create(input: input, log: log));
        Type.Execute(new HotkeyStep(KeyModifiers.Control, KeyCode.T, KeyModifiers.Alt), StepContexts.Create(input: input));

        Assert.Equal(StepResult.Done, result);
        Assert.Equal(["hotkey Control, Alt+F9 right Alt", "hotkey Control+T"], input.Calls);
        Assert.Contains(new LogProperty("keys", "Ctrl+RAlt+F9"), Assert.Single(log.Events).Properties!);
    }

    [Fact]
    public void AnUnsetStepSkipsWithoutTouchingTheSimulator()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new HotkeyStep(KeyModifiers.Control, KeyCode.None), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no key set", result.Reason);
        Assert.Empty(input.Calls);
    }

    [Theory]
    [InlineData(SimulationResult.Unsupported, "Win+L: Unsupported")]
    [InlineData(SimulationResult.Failed, "Win+L: Failed")]
    public void ASimulatorRefusalFailsTheStepWithItsAnswer(SimulationResult answer, string expectedReason)
    {
        var input = new FakeInputSimulator { OtherResult = answer };

        var result = Type.Execute(new HotkeyStep(KeyModifiers.Meta, KeyCode.L), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Single(input.Calls);
    }
}
