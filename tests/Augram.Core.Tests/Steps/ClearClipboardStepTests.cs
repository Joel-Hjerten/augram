using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;
using Augram.Core.Steps.Delay;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class ClearClipboardStepTests
{
    private static readonly ClearClipboardStepType Type = ClearClipboardStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("clearClipboard", Type.Key);
        Assert.Equal("Clear clipboard", Type.DisplayName);
        Assert.Equal(StepCategory.System, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<ClearClipboardStep>(Type.CreateDefault());
        Assert.Equal("Clear clipboard", step.Summary);
        Assert.Equal("Clear clipboard", ((IStep)step).SummaryOn(HostPlatform.MacOS));
        Assert.Same(Type, step.Type);
        Assert.Equal(new ClearClipboardStep(), step);
    }

    [Fact]
    public void ItHasNoParametersAndIgnoresMembersItDoesNotKnow()
    {
        Assert.Equal("{}", Type.Write(new ClearClipboardStep()).ToJsonString());
        Assert.Equal(new ClearClipboardStep(), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new ClearClipboardStep(), Type.Read(StepJson.Object("""{ "what": "everything" }""")));
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
    }

    [Fact]
    public void ItIsTheSameOnEveryPlatform()
    {
        var step = new ClearClipboardStep();

        Assert.Equal(StepConversionKind.Unchanged, Type.Convert(step, HostPlatform.Windows, HostPlatform.MacOS).Kind);
    }

    [Fact]
    public void AClearedClipboardIsDone()
    {
        var clipboard = new FakeClipboard();

        var result = Type.Execute(new ClearClipboardStep(), Context(clipboard));

        Assert.True(result.Succeeded);
        Assert.Equal(1, clipboard.Clears);
    }

    [Fact]
    public void NoClipboardOnThisPlatformSkips_SoTheCommandGoesOn()
    {
        var result = Type.Execute(new ClearClipboardStep(), StepContexts.Create());

        Assert.Equal((StepOutcome.Skipped, NullClipboard.Reason), (result.Outcome, result.Reason));
    }

    [Fact]
    public void AClipboardHeldByAnotherAppFails_SoTheCommandStops()
    {
        var clipboard = new FakeClipboard { Answer = ClipboardResult.Failed("the clipboard is held by another app") };

        var result = Type.Execute(new ClearClipboardStep(), Context(clipboard));

        Assert.Equal((StepOutcome.Failed, "the clipboard is held by another app"), (result.Outcome, result.Reason));
    }

    [Fact]
    public void EveryRunLogsOneDebugLine()
    {
        var log = new CountingEventLog();

        Type.Execute(new ClearClipboardStep(), StepContexts.Create(log: log) with { Clipboard = new FakeClipboard() });

        var entry = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Clear clipboard"), (entry.Level, entry.Source, entry.Message));
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), entry.Properties!);
    }

    private static StepExecutionContext Context(FakeClipboard clipboard) => StepContexts.Create() with { Clipboard = clipboard };
}
