using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The Remap step's metadata, summaries, JSON, conversion ("same on both") and its refusal to run as a command step.</summary>
public sealed class RemapStepTests
{
    private static readonly RemapStepType Type = RemapStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal(("remap", "Remap", StepCategory.Mouse), (Type.Key, Type.DisplayName, Type.Category));
        Assert.True(Type.IsPlatformNeutral);
        Assert.True(Type.HoldRemapsOnly);

        var step = Assert.IsType<RemapStep>(Type.CreateDefault());
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Middle)), step);
        Assert.Equal("Remap to Middle", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Fact]
    public void SummaryNamesTheOutputAsEachPlatformDoes()
    {
        Assert.Equal("Remap to Ctrl + Middle", new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control)).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Remap to Shift + Middle", new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)).SummaryOn(HostPlatform.MacOS));
        Assert.Equal("Remap to G", new RemapStep(new RemapOutput.Key(KeyCode.G)).Summary);
        Assert.Equal("Remap to Ctrl+Alt+Shift+R", new RemapStep(new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Remap to Ctrl+Opt+Shift+R", new RemapStep(new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)).SummaryOn(HostPlatform.MacOS));
        Assert.Equal("Remap to RAlt+F9", new RemapStep(new RemapOutput.Key(KeyCode.F9, KeyModifiers.Alt, KeyModifiers.Alt)).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Remap to Num .", new RemapStep(new RemapOutput.Key(KeyCode.NumPadDecimal)).Summary);
        Assert.Equal("Remap to Ctrl + wheel up", new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control)).SummaryOn(HostPlatform.Windows));
        Assert.Equal("Remap to wheel right", new RemapStep(new RemapOutput.Wheel(ScrollDirection.Right)).Summary);
        Assert.Equal("Remap (no key set)", new RemapStep(new RemapOutput.Key(KeyCode.None)).Summary);
    }

    public static TheoryData<RemapOutput, string> Outputs => new()
    {
        { new RemapOutput.Button(MouseButton.Middle), """{"output":"Button","button":"Middle"}""" },
        { new RemapOutput.Button(MouseButton.X2, KeyModifiers.Control | KeyModifiers.Shift), """{"output":"Button","button":"X2","modifiers":"Control, Shift"}""" },
        { new RemapOutput.Key(KeyCode.G), """{"output":"Key","key":"G"}""" },
        { new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, KeyModifiers.Alt), """{"output":"Key","key":"R","modifiers":"Control, Alt, Shift","rightHand":"Alt"}""" },
        { new RemapOutput.Key(KeyCode.None), """{"output":"Key","key":"None"}""" },
        { new RemapOutput.Wheel(ScrollDirection.Down), """{"output":"Wheel","direction":"Down"}""" },
        { new RemapOutput.Wheel(ScrollDirection.Left, KeyModifiers.Meta), """{"output":"Wheel","direction":"Left","modifiers":"Meta"}""" },
    };

    [Theory]
    [MemberData(nameof(Outputs))]
    public void EveryOutputRoundTripsByteStable(RemapOutput output, string json)
    {
        var step = new RemapStep(output);

        var written = Type.Write(step);

        Assert.Equal(json, written.ToJsonString());
        Assert.Equal(step, Type.Read(written));
        Assert.Equal(json, Type.Write(Type.Read(written)).ToJsonString());
    }

    [Fact]
    public void ReadTakesDefaultsWhenAbsent_NamesIgnoringCase_RightHandCutToTheModifiers()
    {
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Middle)), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None)), Type.Read(StepJson.Object("""{ "output": "key" }""")));
        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up)), Type.Read(StepJson.Object("""{ "output": "WHEEL" }""")));
        Assert.Equal(
            new RemapStep(new RemapOutput.Key(KeyCode.P, KeyModifiers.Control, KeyModifiers.Control)),
            Type.Read(StepJson.Object("""{ "output": "Key", "key": "p", "modifiers": "control", "rightHand": "Control, Shift" }""")));
        Assert.Equal("""{"output":"Button","button":"Left"}""", Type.Write(new RemapStep(new RemapOutput.Button(MouseButton.Left, (KeyModifiers)0x30))).ToJsonString());
    }

    [Theory]
    [InlineData("""{ "output": "Pen" }""", "'output' must be one of Button, Key, Wheel; got 'Pen'.")]
    [InlineData("""{ "output": "Button", "button": "Thumb" }""", "'button' must be one of Left, Middle, Right, X1, X2; got 'Thumb'.")]
    [InlineData("""{ "output": "Key", "key": 7 }""", "'key' must be a string.")]
    [InlineData("""{ "output": "Wheel", "direction": "Sideways" }""", "'direction' must be one of Up, Down, Left, Right; got 'Sideways'.")]
    [InlineData("""{ "modifiers": "Hyper" }""", "'modifiers' must list Control, Alt, Shift, Meta separated by commas, or None; got 'Hyper'.")]
    public void BadInputNamesTheMember(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData(HostPlatform.Windows, HostPlatform.MacOS)]
    [InlineData(HostPlatform.MacOS, HostPlatform.Windows)]
    public void TheSameOnBothPlatforms_CtrlStaysCtrl(HostPlatform from, HostPlatform to)
    {
        var step = new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control));

        var conversion = Type.Convert(step, from, to);

        Assert.Equal(StepConversionKind.Unchanged, conversion.Kind);
        Assert.Same(step, conversion.Step);
    }

    [Fact]
    public void TheCommandExecutorNeverRunsIt_SkippedWithTheReason_OneDebugLine_NoInput()
    {
        var log = new CountingEventLog();
        var input = new FakeInputSimulator();

        var result = Type.Execute(new RemapStep(new RemapOutput.Key(KeyCode.G)), StepContexts.Create(input: input, log: log));

        Assert.Equal(StepResult.Skipped(RemapStepType.NotRunHere), result);
        Assert.Empty(input.Calls);
        var entry = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Remap"), (entry.Level, entry.Source, entry.Message));
        Assert.Contains(new LogProperty("output", "G"), entry.Properties!);
    }

    [Fact]
    public void WriteConvertAndExecuteRefuseAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
        Assert.Throws<ArgumentException>(() => Type.Convert(new DelayStep(1), HostPlatform.Windows, HostPlatform.MacOS));
        Assert.Throws<ArgumentException>(() => Type.Execute(new DelayStep(1), StepContexts.Create()));
    }
}
