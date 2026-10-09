using System.Text.Json.Nodes;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Unknown;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class UnknownStepTests
{
    private static readonly UnknownStepType Type = UnknownStepType.Instance;

    [Fact]
    public void MetadataAndSummary()
    {
        Assert.Equal("unknown", Type.Key);
        Assert.Equal(StepCategory.Other, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = UnknownStepType.Keep("scroll", new JsonObject { ["direction"] = "Up" }, "unknown step type 'scroll'");
        Assert.Same(Type, step.Type);
        Assert.Equal("'scroll' step (needs a newer Augram)", step.Summary);
    }

    [Fact]
    public void WritesBackTheKeyAndParametersItWasReadWith()
    {
        var parameters = new JsonObject { ["direction"] = "Up", ["notches"] = 3, ["keys"] = new JsonArray("Control") };

        var step = UnknownStepType.Keep("scroll", parameters, "unknown step type 'scroll'");

        Assert.Equal("scroll", ((IStep)step).StoredKey);
        Assert.Equal(parameters.ToJsonString(), Type.Write(step).ToJsonString());
        Assert.Equal(step, UnknownStepType.Keep("scroll", Type.Write(step), "unknown step type 'scroll'"));
    }

    [Fact]
    public void ExecuteSkipsWithTheReasonAndLogsOneInfoLine()
    {
        var log = new CountingEventLog();
        var windows = new FakeWindowOperations();
        var input = new FakeInputSimulator();

        var result = Type.Execute(
            UnknownStepType.Keep("scroll", [], "unknown step type 'scroll'"),
            StepContexts.Create(StepContexts.Window(), windows, input, log));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("this Augram cannot run it (unknown step type 'scroll'); update Augram", result.Reason);
        Assert.Empty(windows.Calls);
        Assert.Empty(input.Calls);
        var entry = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Info, "Unknown step skipped"), (entry.Level, entry.Message));
        Assert.Contains(new LogProperty("type", "scroll"), entry.Properties!);
    }
}
