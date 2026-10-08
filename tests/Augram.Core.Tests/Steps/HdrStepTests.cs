using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hdr;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The HDR step: metadata, summaries, JSON, the macOS conversion and every execute branch against a fake adapter.</summary>
public sealed class HdrStepTests
{
    private static readonly HdrStepType Type = HdrStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("hdr", Type.Key);
        Assert.Equal("HDR", Type.DisplayName);
        Assert.Equal(StepCategory.Display, Type.Category);
        Assert.False(Type.IsPlatformNeutral);

        var step = Assert.IsType<HdrStep>(Type.CreateDefault());
        Assert.Equal(new HdrStep(HdrAction.Toggle, DisplayTarget.UnderGesture), step);
        Assert.Equal("Toggle HDR", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(HdrAction.Toggle, DisplayTarget.UnderGesture, "Toggle HDR", """{"action":"Toggle","display":"UnderGesture"}""")]
    [InlineData(HdrAction.On, DisplayTarget.UnderGesture, "HDR on", """{"action":"On","display":"UnderGesture"}""")]
    [InlineData(HdrAction.Off, DisplayTarget.Main, "HDR off (main display)", """{"action":"Off","display":"Main"}""")]
    public void SummaryAndJsonRoundTrip(HdrAction action, DisplayTarget target, string summary, string json)
    {
        var step = new HdrStep(action, target);

        Assert.Equal(summary, step.Summary);
        Assert.Equal(json, Type.Write(step).ToJsonString());
        Assert.Equal(step, Type.Read(StepJson.Object(json)));
    }

    [Theory]
    [InlineData("""{"action":"Flip"}""", "'action' must be one of Toggle, On, Off; got 'Flip'.")]
    [InlineData("""{"action":1}""", "'action' must be a string.")]
    [InlineData("""{"display":"Second"}""", "'display' must be one of UnderGesture, Main; got 'Second'.")]
    public void BadParametersNameTheMember(string json, string message)
    {
        Assert.Equal(message, Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json))).Message);
    }

    [Fact]
    public void MissingMembersTakeTheirDefaults()
    {
        Assert.Equal(new HdrStep(), Type.Read(StepJson.Object("{}")));
    }

    [Fact]
    public void OnMacOSThereIsNoEquivalent_OnWindowsItIsUnchanged()
    {
        var step = new HdrStep(HdrAction.On);

        var mac = Type.Convert(step, HostPlatform.Windows, HostPlatform.MacOS);
        Assert.Equal(StepConversionKind.NotConvertible, mac.Kind);
        Assert.Equal("HDR on has no macOS equivalent: macOS offers apps no way to switch HDR", mac.Reason);
        Assert.Equal(StepConversion.Same(step), Type.Convert(step, HostPlatform.MacOS, HostPlatform.Windows));
        Assert.Equal(StepConversion.Same(step), Type.Convert(step, HostPlatform.Windows, HostPlatform.Windows));
    }

    [Theory]
    [InlineData(HdrAction.Toggle, HdrState.Off, @"hdr \\.\DISPLAY1 on")]
    [InlineData(HdrAction.Toggle, HdrState.On, @"hdr \\.\DISPLAY1 off")]
    [InlineData(HdrAction.On, HdrState.Off, @"hdr \\.\DISPLAY1 on")]
    [InlineData(HdrAction.Off, HdrState.On, @"hdr \\.\DISPLAY1 off")]
    public void ItSwitchesTheDisplayUnderTheGesture(HdrAction action, HdrState state, string call)
    {
        var displays = Displays(FakeDisplayModes.Tv(hdr: state));

        var result = Type.Execute(new HdrStep(action), Context(displays));

        Assert.True(result.Succeeded);
        Assert.Equal([call], displays.Calls);
    }

    [Theory]
    [InlineData(HdrAction.On, HdrState.On)]
    [InlineData(HdrAction.Off, HdrState.Off)]
    public void AlreadyThereIsDoneWithoutAChange(HdrAction action, HdrState state)
    {
        var displays = Displays(FakeDisplayModes.Tv(hdr: state));

        Assert.True(Type.Execute(new HdrStep(action), Context(displays)).Succeeded);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void APlatformThatCannotSwitchHdrSkipsWithItsName()
    {
        var displays = Displays(FakeDisplayModes.Tv());
        displays.CanSwitchHdr = false;
        displays.Platform = HostPlatform.MacOS;

        var result = Type.Execute(new HdrStep(), Context(displays));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("switching HDR is not supported on MacOS", result.Reason);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void ADisplayWithoutHdrIsSkipped()
    {
        var displays = Displays(FakeDisplayModes.Tv(name: "Office monitor", hdr: HdrState.Unsupported));

        var result = Type.Execute(new HdrStep(HdrAction.On), Context(displays));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("Office monitor does not support HDR", result.Reason);
    }

    [Fact]
    public void NoDisplayUnderTheStartIsSkipped_MainStillWorks()
    {
        var displays = Displays(FakeDisplayModes.Tv(bounds: new DisplayBounds(5000, 0, 100, 100)));

        Assert.Equal("no display under the gesture start", Type.Execute(new HdrStep(), Context(displays)).Reason);
        Assert.True(Type.Execute(new HdrStep(HdrAction.On, DisplayTarget.Main), Context(displays)).Succeeded);
        Assert.Single(displays.Calls);
    }

    [Fact]
    public void TheNullAdapterSkips()
    {
        var result = Type.Execute(new HdrStep(), StepContexts.Create());

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.StartsWith("switching HDR is not supported on ", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAdapterFailureFails()
    {
        var displays = Displays(FakeDisplayModes.Tv());
        displays.Result = DisplayChangeResult.Failed("DisplayConfigSetDeviceInfo failed (87)");

        var result = Type.Execute(new HdrStep(HdrAction.On), Context(displays));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("DisplayConfigSetDeviceInfo failed (87)", result.Reason);

        displays.Result = new DisplayChangeResult(false);
        Assert.Equal("SONY TV did not switch HDR on", Type.Execute(new HdrStep(HdrAction.On), Context(displays)).Reason);
    }

    private static FakeDisplayModes Displays(DisplayInfo display)
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(display);
        return displays;
    }

    private static StepExecutionContext Context(FakeDisplayModes displays) => StepContexts.Create() with { Displays = displays };
}
