using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Tests.Diagnostics;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class ImportedStepTests
{
    private static readonly ImportedStepType Type = ImportedStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("imported", Type.Key);
        Assert.Equal("Imported (not supported yet)", Type.DisplayName);
        Assert.Equal(StepCategory.Other, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<ImportedStep>(Type.CreateDefault());
        Assert.Equal(string.Empty, step.SourceMethod);
        Assert.Equal(string.Empty, step.Description);
        Assert.Empty(step.Parameters);
        Assert.Equal("Imported step (not supported yet)", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Fact]
    public void SummaryNamesTheSourceMethod()
    {
        Assert.Equal("SendAltDown (not supported yet)", new ImportedStep("SendAltDown", "Hold Alt", ImportedStep.NoParameters).Summary);
    }

    [Fact]
    public void RoundTripsMethodDescriptionAndEveryParameter()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["modifiers"] = "2",
            ["Key"] = "87",
        };
        var step = new ImportedStep("SendHotKey", "Close tab", parameters);

        var written = Type.Write(step);
        var reread = Assert.IsType<ImportedStep>(Type.Read(written));

        Assert.Equal(
            """{"method":"SendHotKey","description":"Close tab","parameters":{"modifiers":"2","Key":"87"}}""",
            written.ToJsonString());
        Assert.Equal("SendHotKey", reread.SourceMethod);
        Assert.Equal("Close tab", reread.Description);
        Assert.Equal(parameters, reread.Parameters);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Fact]
    public void ParameterValuesWithQuotesAndNonAsciiSurviveTheRoundTrip()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hotkey"] = """{"modifiers":2,"Key":87}""",
            ["text"] = "fov 67.5 → ↑",
            ["empty"] = string.Empty,
        };

        var reread = Assert.IsType<ImportedStep>(Type.Read(Type.Write(new ImportedStep("SendKeys", string.Empty, parameters))));

        Assert.Equal(parameters, reread.Parameters);
    }

    [Fact]
    public void ReadTakesDefaultsForMissingMembers()
    {
        var reread = Assert.IsType<ImportedStep>(Type.Read(StepJson.Object("""{ "method": "ConsumePhysicalInput" }""")));

        Assert.Equal("ConsumePhysicalInput", reread.SourceMethod);
        Assert.Equal(string.Empty, reread.Description);
        Assert.Empty(reread.Parameters);
        Assert.Equal(Type.CreateDefault(), Type.Read(StepJson.Object("{}")));
        Assert.Equal(Type.CreateDefault(), Type.Read(StepJson.Object("""{ "parameters": {} }""")));
    }

    [Theory]
    [InlineData("""{ "method": 5 }""", "'method' must be a string.")]
    [InlineData("""{ "description": [] }""", "'description' must be a string.")]
    [InlineData("""{ "parameters": "x=1" }""", "'parameters' must be an object.")]
    [InlineData("""{ "parameters": { "count": 3 } }""", "'parameters.count' must be a string.")]
    [InlineData("""{ "parameters": { "count": null } }""", "'parameters.count' must be a string.")]
    [InlineData("""{ "parameters": { "hotkey": { "Key": 87 } } }""", "'parameters.hotkey' must be a string.")]
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
    public void ExecuteSkipsNamingTheMethodAndLogsOneInfoLine()
    {
        var log = new CountingEventLog();
        var windows = new FakeWindowOperations();
        var input = new FakeInputSimulator();

        var result = Type.Execute(
            new ImportedStep("SendWinDown", "Hold Win", ImportedStep.NoParameters),
            StepContexts.Create(StepContexts.Window(), windows, input, log));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("imported step 'SendWinDown' is not supported yet", result.Reason);
        Assert.Empty(windows.Calls);
        Assert.Empty(input.Calls);

        var entry = Assert.Single(log.Events);
        Assert.Equal(EventLevel.Info, entry.Level);
        Assert.Equal("steps", entry.Source);
        Assert.Equal("Imported step skipped", entry.Message);
        Assert.Contains(new LogProperty("method", "SendWinDown"), entry.Properties!);
        Assert.Contains(new LogProperty("description", "Hold Win"), entry.Properties!);
    }
}
