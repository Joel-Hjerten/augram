using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Scroll;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The Scroll step's metadata, summaries, JSON and conversion; execution is in <see cref="ScrollExecutionTests"/>.</summary>
public sealed class ScrollStepTests
{
    private static readonly ScrollStepType Type = ScrollStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("scroll", Type.Key);
        Assert.Equal("Scroll", Type.DisplayName);
        Assert.Equal(StepCategory.Mouse, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<ScrollStep>(Type.CreateDefault());
        Assert.Equal(new ScrollStep(ScrollDirection.Down, 1, KeyModifiers.None), step);
        Assert.Equal("Scroll down", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(ScrollDirection.Up, 1, KeyModifiers.None, "Scroll up", "Scroll up")]
    [InlineData(ScrollDirection.Down, 3, KeyModifiers.Control, "Ctrl + scroll down ×3", "Ctrl + scroll down ×3")]
    [InlineData(ScrollDirection.Right, 1, KeyModifiers.None, "Scroll right", "Scroll right")]
    [InlineData(ScrollDirection.Left, 2, KeyModifiers.Control | KeyModifiers.Shift, "Ctrl+Shift + scroll left ×2", "Ctrl+Shift + scroll left ×2")]
    [InlineData(ScrollDirection.Up, 1, KeyModifiers.Meta | KeyModifiers.Alt, "Alt+Win + scroll up", "Opt+Cmd + scroll up")]
    public void SummaryNamesTheKeysAsEachPlatformDoes(ScrollDirection direction, int notches, KeyModifiers keys, string windows, string mac)
    {
        var step = new ScrollStep(direction, notches, keys);

        Assert.Equal(windows, step.SummaryOn(HostPlatform.Windows));
        Assert.Equal(mac, step.SummaryOn(HostPlatform.MacOS));
    }

    [Fact]
    public void BitsOutsideTheModifiersAndNotchesOutOfRangeMeanNothing()
    {
        var step = new ScrollStep(ScrollDirection.Up, 99, (KeyModifiers)0x30 | KeyModifiers.Shift);

        Assert.Equal(KeyModifiers.Shift, step.HeldKeys);
        Assert.Equal(ScrollStepType.MaxNotches, step.NotchCount);
        Assert.Equal(1, new ScrollStep(ScrollDirection.Up, 0).NotchCount);
        Assert.Equal("""{"direction":"Up","notches":20,"keys":"Shift"}""", Type.Write(step).ToJsonString());
    }

    [Theory]
    [InlineData(ScrollDirection.Down, 1, KeyModifiers.None, """{"direction":"Down","notches":1}""")]
    [InlineData(ScrollDirection.Up, 1, KeyModifiers.Control, """{"direction":"Up","notches":1,"keys":"Control"}""")]
    [InlineData(ScrollDirection.Right, 20, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, """{"direction":"Right","notches":20,"keys":"Control, Alt, Shift, Meta"}""")]
    [InlineData(ScrollDirection.Left, 5, KeyModifiers.Shift, """{"direction":"Left","notches":5,"keys":"Shift"}""")]
    public void EveryParameterRoundTripsByteStable(ScrollDirection direction, int notches, KeyModifiers keys, string json)
    {
        var step = new ScrollStep(direction, notches, keys);

        var written = Type.Write(step);

        Assert.Equal(json, written.ToJsonString());
        Assert.Equal(step, Type.Read(written));
        Assert.Equal(json, Type.Write(Type.Read(written)).ToJsonString());
    }

    [Fact]
    public void ReadTakesDefaultsWhenAbsentAndNamesIgnoringCase()
    {
        Assert.Equal(new ScrollStep(ScrollDirection.Down), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new ScrollStep(ScrollDirection.Left, 2, KeyModifiers.Control | KeyModifiers.Shift), Type.Read(StepJson.Object("""{ "direction": "left", "notches": 2, "keys": "control, SHIFT" }""")));
        Assert.Equal(new ScrollStep(ScrollDirection.Up), Type.Read(StepJson.Object("""{ "direction": "Up", "keys": "None" }""")));
    }

    [Theory]
    [InlineData("""{ "direction": "Sideways" }""", "'direction' must be one of Up, Down, Left, Right; got 'Sideways'.")]
    [InlineData("""{ "direction": 1 }""", "'direction' must be a string.")]
    [InlineData("""{ "notches": 0 }""", "'notches' must be between 1 and 20; got 0.")]
    [InlineData("""{ "notches": 21 }""", "'notches' must be between 1 and 20; got 21.")]
    [InlineData("""{ "notches": "3" }""", "'notches' must be an integer.")]
    [InlineData("""{ "keys": "Hyper" }""", "'keys' must list Control, Alt, Shift, Meta separated by commas, or None; got 'Hyper'.")]
    [InlineData("""{ "keys": 1 }""", "'keys' must be a string.")]
    public void BadInputNamesTheMember(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void WriteAndExecuteRefuseAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
        Assert.Throws<ArgumentException>(() => Type.Execute(new DelayStep(1), StepContexts.Create()));
    }

    [Theory]
    [InlineData(HostPlatform.Windows, HostPlatform.MacOS, KeyModifiers.None)]
    [InlineData(HostPlatform.Windows, HostPlatform.MacOS, KeyModifiers.Shift | KeyModifiers.Alt)]
    [InlineData(HostPlatform.MacOS, HostPlatform.Windows, KeyModifiers.Control)]
    [InlineData(HostPlatform.Windows, HostPlatform.Windows, KeyModifiers.Meta)]
    public void WithoutCtrlOrCmdTheStepRunsUnchanged(HostPlatform from, HostPlatform to, KeyModifiers keys)
    {
        var step = new ScrollStep(ScrollDirection.Up, 2, keys);

        var conversion = Type.Convert(step, from, to);

        Assert.Equal(StepConversionKind.Unchanged, conversion.Kind);
        Assert.Same(step, conversion.Step);
    }

    [Theory]
    [InlineData(HostPlatform.Windows, KeyModifiers.Control, KeyModifiers.Meta)]
    [InlineData(HostPlatform.Windows, KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Meta | KeyModifiers.Shift)]
    [InlineData(HostPlatform.MacOS, KeyModifiers.Meta, KeyModifiers.Control)]
    [InlineData(HostPlatform.MacOS, KeyModifiers.Meta | KeyModifiers.Alt, KeyModifiers.Control | KeyModifiers.Alt)]
    public void CtrlAndCmdSwapLikeAHotkeys(HostPlatform from, KeyModifiers keys, KeyModifiers expected)
    {
        var to = from == HostPlatform.Windows ? HostPlatform.MacOS : HostPlatform.Windows;

        var conversion = Type.Convert(new ScrollStep(ScrollDirection.Down, 3, keys), from, to);

        Assert.Equal(StepConversionKind.Converted, conversion.Kind);
        Assert.Equal(new ScrollStep(ScrollDirection.Down, 3, expected), conversion.Step);
    }

    [Fact]
    public void TheWindowsKeyAndCmdWithCtrlHaveNoCounterpart()
    {
        var win = Type.Convert(new ScrollStep(ScrollDirection.Down, 1, KeyModifiers.Meta), HostPlatform.Windows, HostPlatform.MacOS);
        var both = Type.Convert(new ScrollStep(ScrollDirection.Up, 1, KeyModifiers.Meta | KeyModifiers.Control), HostPlatform.MacOS, HostPlatform.Windows);

        Assert.Equal((StepConversionKind.NotConvertible, "Win + scroll down needs a macOS version: the Windows key has no Mac counterpart"), (win.Kind, win.Reason));
        Assert.Equal((StepConversionKind.NotConvertible, "Ctrl+Cmd + scroll up needs a Windows version: Cmd and Ctrl together have no Windows counterpart"), (both.Kind, both.Reason));
    }

    [Fact]
    public void EveryRunLogsOneDebugLine()
    {
        var log = new CountingEventLog();

        Type.Execute(new ScrollStep(ScrollDirection.Left, 2, KeyModifiers.Shift), StepContexts.Create(log: log));

        var entry = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Scroll"), (entry.Level, entry.Source, entry.Message));
        Assert.Contains(new LogProperty("direction", ScrollDirection.Left), entry.Properties!);
        Assert.Contains(new LogProperty("notches", 2), entry.Properties!);
        Assert.Contains(new LogProperty("keys", KeyModifiers.Shift), entry.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), entry.Properties!);
    }
}
