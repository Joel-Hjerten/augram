using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.DisplayMode;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>Every branch of the Display mode executor, against a fake display adapter that changes nothing real.</summary>
public sealed class DisplayModeExecutionTests
{
    private static readonly DisplayModeStepType Type = DisplayModeStepType.Instance;

    [Fact]
    public void BothAutoIsSkippedWithoutReadingTheDisplays()
    {
        var displays = new FakeDisplayModes();

        var result = Run(new DisplayModeStep(), displays);

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("nothing to change: resolution and refresh are both Auto", result.Reason);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void NoDisplaysIsSkippedWithThePlatform()
    {
        var result = Run(Rate(24), new FakeDisplayModes { Platform = HostPlatform.MacOS });

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no displays reported on MacOS", result.Reason);
    }

    [Fact]
    public void TheNullAdapterSkipsToo()
    {
        var context = StepContexts.Create();

        var result = Type.Execute(Rate(24), context);

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.StartsWith("no displays reported on ", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AStartOnNoDisplayIsSkipped()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv(bounds: new DisplayBounds(1000, 1000, 100, 100)));

        var result = Run(Rate(24), displays);

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no display under the gesture start", result.Reason);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void TheDisplayUnderTheStartIsChanged()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv(@"\\.\DISPLAY1", isMain: true, bounds: new DisplayBounds(0, 0, 50, 50)));
        displays.Listed.Add(FakeDisplayModes.Tv(@"\\.\DISPLAY2", isMain: false, bounds: new DisplayBounds(50, 0, 3840, 2160)));

        var result = Run(Rate(24), displays);

        Assert.True(result.Succeeded);
        Assert.Equal([@"mode \\.\DISPLAY2 3840×2160 at 24 Hz"], displays.Calls);
    }

    [Fact]
    public void MainChangesTheMainDisplayWhereverTheGestureWas()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv(@"\\.\DISPLAY2", isMain: false, bounds: new DisplayBounds(0, 0, 3840, 2160)));
        displays.Listed.Add(FakeDisplayModes.Tv(@"\\.\DISPLAY1", isMain: true, bounds: new DisplayBounds(3840, 0, 3840, 2160)));

        var result = Run(new DisplayModeStep(new DisplayResolution(1920, 1080), RefreshRate.FromHertz(119.88), DisplayTarget.Main), displays);

        Assert.True(result.Succeeded);
        Assert.Equal([@"mode \\.\DISPLAY1 1920×1080 at 119.88 Hz"], displays.Calls);
    }

    [Fact]
    public void AModeTheDisplayLacksIsSkippedWithWhatItOffersAndNothingIsApplied()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv(legacyRates: [60, 120]));

        var result = Run(Rate(24), displays);

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("SONY TV has no 24 Hz at 3840×2160; it offers 120, 60 Hz", result.Reason);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void TheCurrentModeIsDoneWithoutAChange()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv());

        var result = Run(Rate(120), displays);

        Assert.True(result.Succeeded);
        Assert.Empty(displays.Calls);
    }

    [Fact]
    public void AnAdapterFailureFailsTheStepWithItsReason()
    {
        var displays = new FakeDisplayModes { Result = DisplayChangeResult.Failed("Windows refused it") };
        displays.Listed.Add(FakeDisplayModes.Tv());

        var result = Run(Rate(60), displays);

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("Windows refused it", result.Reason);
        Assert.Single(displays.Calls);
    }

    [Fact]
    public void AFailureWithoutAReasonStillSaysWhat()
    {
        var displays = new FakeDisplayModes { Result = new DisplayChangeResult(false) };
        displays.Listed.Add(FakeDisplayModes.Tv());

        var result = Run(Rate(60), displays);

        Assert.Equal("SONY TV did not change to 3840×2160 at 60 Hz", result.Reason);
    }

    [Fact]
    public void OneDebugLineNamesTheDisplayAndBothModes()
    {
        var displays = new FakeDisplayModes();
        displays.Listed.Add(FakeDisplayModes.Tv());
        var log = new CountingEventLog();

        Type.Execute(Rate(24), StepContexts.Create(log: log) with { Displays = displays });

        var line = Assert.Single(log.Events);
        Assert.Equal(EventLevel.Debug, line.Level);
        Assert.Equal("steps", line.Source);
        Assert.Equal("Display mode", line.Message);
        Assert.Contains(new LogProperty("display", "SONY TV"), line.Properties!);
        Assert.Contains(new LogProperty("from", "3840×2160 at 120 Hz"), line.Properties!);
        Assert.Contains(new LogProperty("to", "3840×2160 at 24 Hz"), line.Properties!);
    }

    private static DisplayModeStep Rate(double hertz) => new(Refresh: RefreshRate.FromHertz(hertz));

    /// <summary>The fake start point is (100, 200); every test display covers it unless the test moves it.</summary>
    private static StepResult Run(DisplayModeStep step, FakeDisplayModes displays)
        => Type.Execute(step, StepContexts.Create() with { Displays = displays });
}
