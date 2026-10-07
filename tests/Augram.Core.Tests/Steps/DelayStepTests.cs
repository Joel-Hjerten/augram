using System.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class DelayStepTests
{
    private static readonly DelayStepType Type = DelayStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("delay", Type.Key);
        Assert.Equal("Delay", Type.DisplayName);
        Assert.Equal(StepCategory.Timing, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<DelayStep>(Type.CreateDefault());
        Assert.Equal(30, step.Milliseconds);
        Assert.Equal("Wait 30 ms", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(700)]
    [InlineData(60_000)]
    public void RoundTripsTheMilliseconds(int milliseconds)
    {
        var step = new DelayStep(milliseconds);

        var written = Type.Write(step);

        Assert.Equal($$"""{"milliseconds":{{milliseconds}}}""", written.ToJsonString());
        Assert.Equal(step, Type.Read(written));
    }

    [Fact]
    public void ReadTakesTheDefaultWhenAbsent()
    {
        Assert.Equal(new DelayStep(30), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new DelayStep(30), Type.Read(StepJson.Object("""{ "milliseconds": null, "seconds": 2 }""")));
    }

    [Theory]
    [InlineData("""{ "milliseconds": -1 }""", "'milliseconds' must be between 0 and 60000; got -1.")]
    [InlineData("""{ "milliseconds": 60001 }""", "'milliseconds' must be between 0 and 60000; got 60001.")]
    [InlineData("""{ "milliseconds": "30" }""", "'milliseconds' must be an integer.")]
    [InlineData("""{ "milliseconds": 1.5 }""", "'milliseconds' must be an integer.")]
    public void BadInputNamesTheMember(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void WriteRefusesAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new MediaKeyStep(MediaKeyKind.Stop)));
    }

    [Fact]
    public void ZeroReturnsDoneAtOnceEvenWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = Type.Execute(new DelayStep(0), StepContexts.Create(cancellation: cts.Token));

        Assert.Same(StepResult.Done, result);
    }

    [Fact]
    public void AShortWaitCompletesAsDone()
    {
        var watch = Stopwatch.StartNew();

        var result = Type.Execute(new DelayStep(20), StepContexts.Create());

        Assert.True(result.Succeeded);
        Assert.True(watch.ElapsedMilliseconds >= 1, $"returned after {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void AnAlreadyCancelledCommandSkipsWithoutWaiting()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var watch = Stopwatch.StartNew();

        var result = Type.Execute(new DelayStep(60_000), StepContexts.Create(cancellation: cts.Token));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled", result.Reason);
        Assert.True(watch.ElapsedMilliseconds < 10_000, $"returned after {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void CancellationDuringTheWaitWakesItAsSkipped()
    {
        using var cts = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        cts.CancelAfter(20);

        var result = Type.Execute(new DelayStep(60_000), StepContexts.Create(cancellation: cts.Token));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled", result.Reason);
        Assert.True(watch.ElapsedMilliseconds < 10_000, $"returned after {watch.ElapsedMilliseconds} ms");
    }
}
