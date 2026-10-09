using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>The Display mode step's record and type: metadata, summaries, the JSON round trip and every refusal.</summary>
public sealed class DisplayModeStepTests
{
    private static readonly DisplayModeStepType Type = DisplayModeStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("displayMode", Type.Key);
        Assert.Equal("Display mode", Type.DisplayName);
        Assert.Equal(StepCategory.Display, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<DisplayModeStep>(Type.CreateDefault());
        Assert.Null(step.Resolution);
        Assert.Null(step.Refresh);
        Assert.Equal(DisplayTarget.UnderGesture, step.Target);
        Assert.True(step.ChangesNothing);
        Assert.Equal("Display (no change)", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(1920, 1080, 119.88, DisplayTarget.UnderGesture, "Display 1920×1080 at 119.88 Hz")]
    [InlineData(0, 0, 24.0, DisplayTarget.UnderGesture, "Display refresh 24 Hz")]
    [InlineData(3840, 2160, 0.0, DisplayTarget.UnderGesture, "Display 3840×2160")]
    [InlineData(0, 0, 23.976, DisplayTarget.Main, "Display refresh 23.976 Hz (main display)")]
    [InlineData(0, 0, 0.0, DisplayTarget.Main, "Display (no change) (main display)")]
    public void SummariesNameWhatChanges(int width, int height, double hertz, DisplayTarget target, string summary)
    {
        Assert.Equal(summary, Step(width, height, hertz, target).Summary);
    }

    [Theory]
    [InlineData(1920, 1080, 119.88, DisplayTarget.UnderGesture, """{"width":1920,"height":1080,"refreshHz":119.88,"display":"UnderGesture"}""")]
    [InlineData(0, 0, 24.0, DisplayTarget.UnderGesture, """{"refreshHz":24,"display":"UnderGesture"}""")]
    [InlineData(3840, 2160, 0.0, DisplayTarget.Main, """{"width":3840,"height":2160,"display":"Main"}""")]
    [InlineData(0, 0, 23.976, DisplayTarget.Main, """{"refreshHz":23.976,"display":"Main"}""")]
    [InlineData(0, 0, 0.0, DisplayTarget.UnderGesture, """{"display":"UnderGesture"}""")]
    public void WriteEmitsWhatIsSetAndReadsBackTheSameStep(int width, int height, double hertz, DisplayTarget target, string json)
    {
        var step = Step(width, height, hertz, target);

        var written = Type.Write(step);

        Assert.Equal(json, written.ToJsonString());
        Assert.Equal(step, Type.Read(written));
        Assert.Equal(step, Type.Read(StepJson.Object(json)));
        Assert.Equal(json, Type.Write(Type.Read(StepJson.Object(json))).ToJsonString());
    }

    [Fact]
    public void ReadTakesDefaultsAndRoundsTheRateToThreeDecimals()
    {
        Assert.Equal(new DisplayModeStep(), Type.Read(StepJson.Object("{}")));
        Assert.Equal(RefreshRate.FromHertz(119.88), ((DisplayModeStep)Type.Read(StepJson.Object("""{"refreshHz":119.8801198}"""))).Refresh);
        Assert.Equal(DisplayTarget.Main, ((DisplayModeStep)Type.Read(StepJson.Object("""{"display":"main"}"""))).Target);
        Assert.Equal(RefreshRate.FromHertz(60.0), ((DisplayModeStep)Type.Read(new System.Text.Json.Nodes.JsonObject { ["refreshHz"] = 60 })).Refresh);
    }

    [Theory]
    [InlineData("""{"width":1920}""", "'height' is required when 'width' is present.")]
    [InlineData("""{"height":1080}""", "'width' is required when 'height' is present.")]
    [InlineData("""{"width":0,"height":1080}""", "'width' must be between 1 and 32767; got 0.")]
    [InlineData("""{"width":1920,"height":"1080"}""", "'height' must be an integer.")]
    [InlineData("""{"refreshHz":"120"}""", "'refreshHz' must be a number of hertz or \"highest\".")]
    [InlineData("""{"refreshHz":0}""", "'refreshHz' must be above 0 and at most 1000; got 0.")]
    [InlineData("""{"refreshHz":-24}""", "'refreshHz' must be above 0 and at most 1000; got -24.")]
    [InlineData("""{"refreshHz":1001}""", "'refreshHz' must be above 0 and at most 1000; got 1001.")]
    [InlineData("""{"display":"Left"}""", "'display' must be one of UnderGesture, Main; got 'Left'.")]
    public void BadParametersNameTheMember(string json, string message)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void HighestAvailableIsStoredAsTheWordHighestAndReadBack()
    {
        var step = new DisplayModeStep(new DisplayResolution(3840, 2160), HighestRefresh: true);

        var json = Type.Write(step);

        Assert.Equal("highest", (string?)json["refreshHz"]);
        Assert.Equal(step, Type.Read(json));
        Assert.Equal(new DisplayModeStep(HighestRefresh: true), Type.Read(StepJson.Object("""{"refreshHz":"Highest"}""")));
        Assert.Equal("Display 3840×2160 at the highest refresh", step.Summary);
        Assert.Equal("Display refresh highest available (main display)", new DisplayModeStep(Target: DisplayTarget.Main, HighestRefresh: true).Summary);
        Assert.False(step.ChangesNothing);
    }

    [Fact]
    public void ItIsTheSameStepOnEveryPlatform()
    {
        var step = Step(1920, 1080, 24.0, DisplayTarget.UnderGesture);

        Assert.Equal(StepConversion.Same(step), Type.Convert(step, HostPlatform.Windows, HostPlatform.MacOS));
        Assert.Equal(StepConversion.Same(step), Type.Convert(step, HostPlatform.MacOS, HostPlatform.Windows));
    }

    /// <summary>Zero width means Auto resolution, zero hertz Auto refresh.</summary>
    internal static DisplayModeStep Step(int width, int height, double hertz, DisplayTarget target = DisplayTarget.UnderGesture) => new(
        width == 0 ? null : new DisplayResolution(width, height),
        hertz == 0 ? null : RefreshRate.FromHertz(hertz),
        target);
}
