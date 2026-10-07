using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class MediaKeyStepTests
{
    private static readonly MediaKeyStepType Type = MediaKeyStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("mediaKey", Type.Key);
        Assert.Equal("Media key", Type.DisplayName);
        Assert.Equal(StepCategory.System, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<MediaKeyStep>(Type.CreateDefault());
        Assert.Equal(MediaKeyKind.VolumeUp, step.Key);
        Assert.Equal("Volume up", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(MediaKeyKind.VolumeUp, KeyCode.VolumeUp, "Volume up")]
    [InlineData(MediaKeyKind.VolumeDown, KeyCode.VolumeDown, "Volume down")]
    [InlineData(MediaKeyKind.VolumeMute, KeyCode.VolumeMute, "Mute")]
    [InlineData(MediaKeyKind.PlayPause, KeyCode.MediaPlay, "Play/pause")]
    [InlineData(MediaKeyKind.NextTrack, KeyCode.MediaNext, "Next track")]
    [InlineData(MediaKeyKind.PreviousTrack, KeyCode.MediaPrevious, "Previous track")]
    [InlineData(MediaKeyKind.Stop, KeyCode.MediaStop, "Stop")]
    public void EveryKindMapsToOneCodeAndSummaryAndRoundTrips(MediaKeyKind kind, KeyCode expectedCode, string expectedSummary)
    {
        var step = new MediaKeyStep(kind);

        Assert.Equal(expectedCode, kind.ToKeyCode());
        Assert.Equal(expectedSummary, step.Summary);

        var written = Type.Write(step);
        Assert.Equal($$"""{"key":"{{kind}}"}""", written.ToJsonString());
        Assert.Equal(step, Type.Read(written));
    }

    [Fact]
    public void TheMappingCoversTheWholeEnum()
    {
        var codes = Enum.GetValues<MediaKeyKind>().Select(kind => kind.ToKeyCode()).ToArray();

        Assert.Equal(7, codes.Length);
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.DoesNotContain(KeyCode.None, codes);
        Assert.Throws<ArgumentOutOfRangeException>(() => ((MediaKeyKind)99).ToKeyCode());
    }

    [Fact]
    public void ReadTakesTheDefaultWhenAbsentAndMatchesNamesIgnoringCase()
    {
        Assert.Equal(new MediaKeyStep(MediaKeyKind.VolumeUp), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new MediaKeyStep(MediaKeyKind.PlayPause), Type.Read(StepJson.Object("""{ "key": "playpause" }""")));
    }

    [Theory]
    [InlineData("""{ "key": "VolumeLoud" }""", "'key' must be one of VolumeUp, VolumeDown, VolumeMute, PlayPause, NextTrack, PreviousTrack, Stop; got 'VolumeLoud'.")]
    [InlineData("""{ "key": 176 }""", "'key' must be a string.")]
    public void BadInputNamesTheMember(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void WriteRefusesAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
    }

    [Fact]
    public void ExecutePressesThenReleasesTheMappedCode()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new MediaKeyStep(MediaKeyKind.NextTrack), StepContexts.Create(input: input));

        Assert.True(result.Succeeded);
        string[] expected = ["press MediaNext", "release MediaNext"];
        Assert.Equal(expected, input.Calls);
    }

    [Fact]
    public void AFailedPressIsFailedWithTheSimulatorsAnswerAndNoRelease()
    {
        var input = new FakeInputSimulator { PressResult = SimulationResult.Unsupported };

        var result = Type.Execute(new MediaKeyStep(MediaKeyKind.VolumeMute), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("VolumeMute press: Unsupported", result.Reason);
        var call = Assert.Single(input.Calls);
        Assert.Equal("press VolumeMute", call);
    }

    [Fact]
    public void AFailedReleaseIsFailedAfterBothCalls()
    {
        var input = new FakeInputSimulator { ReleaseResult = SimulationResult.Failed };

        var result = Type.Execute(new MediaKeyStep(MediaKeyKind.Stop), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("MediaStop release: Failed", result.Reason);
        Assert.Equal(2, input.Calls.Count);
    }

    [Fact]
    public void EveryRunLogsOneDebugLine()
    {
        var log = new CountingEventLog();

        Type.Execute(new MediaKeyStep(MediaKeyKind.VolumeDown), StepContexts.Create(log: log));

        var entry = Assert.Single(log.Events);
        Assert.Equal(EventLevel.Debug, entry.Level);
        Assert.Equal("steps", entry.Source);
        Assert.Equal("Media key", entry.Message);
        Assert.Contains(new LogProperty("key", MediaKeyKind.VolumeDown), entry.Properties!);
        Assert.Contains(new LogProperty("code", KeyCode.VolumeDown), entry.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), entry.Properties!);
    }
}
