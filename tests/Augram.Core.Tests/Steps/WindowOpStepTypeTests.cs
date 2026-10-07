using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.WindowOp;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class WindowOpStepTypeTests
{
    private static readonly WindowOpStepType Type = WindowOpStepType.Instance;

    [Fact]
    public void MetadataAndDefault()
    {
        Assert.Equal("windowOp", Type.Key);
        Assert.Equal("Window", Type.DisplayName);
        Assert.Equal(StepCategory.System, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<WindowOpStep>(Type.CreateDefault());
        Assert.Equal(WindowOperation.Minimize, step.Operation);
        Assert.Null(step.Size);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData(WindowOperation.Close, "Close window")]
    [InlineData(WindowOperation.Minimize, "Minimize window")]
    [InlineData(WindowOperation.MaximizeOrRestore, "Maximize or restore")]
    [InlineData(WindowOperation.ToggleAlwaysOnTop, "Toggle always on top")]
    [InlineData(WindowOperation.Center, "Center window")]
    [InlineData(WindowOperation.SnapLeftHalf, "Snap to left half")]
    [InlineData(WindowOperation.SnapRightHalf, "Snap to right half")]
    public void SummaryNamesTheOperation(WindowOperation operation, string expected)
    {
        Assert.Equal(expected, new WindowOpStep(operation).Summary);
    }

    [Fact]
    public void SetSizeSummaryShowsTheSize()
    {
        Assert.Equal("Set size 1280×720", new WindowOpStep(WindowOperation.SetSize, new WindowSize(1280, 720)).Summary);
        Assert.Equal("Set size", new WindowOpStep(WindowOperation.SetSize).Summary);
    }

    [Fact]
    public void EveryOperationRoundTripsAndSizeIsWrittenForSetSizeOnly()
    {
        foreach (var operation in Enum.GetValues<WindowOperation>())
        {
            var size = operation == WindowOperation.SetSize ? new WindowSize(1280, 720) : (WindowSize?)null;
            var step = new WindowOpStep(operation, size);

            var written = Type.Write(step);
            var reread = Type.Read(written);

            Assert.Equal(step, reread);
            Assert.Equal(operation == WindowOperation.SetSize, written.ContainsKey("width"));
            Assert.Equal(operation == WindowOperation.SetSize, written.ContainsKey("height"));
            Assert.Equal(operation.ToString(), (string?)written["operation"]);
        }

        Assert.Equal("""{"operation":"SetSize","width":1280,"height":720}""", Type.Write(new WindowOpStep(WindowOperation.SetSize, new WindowSize(1280, 720))).ToJsonString());
        Assert.Equal("""{"operation":"Close"}""", Type.Write(new WindowOpStep(WindowOperation.Close, new WindowSize(1, 1))).ToJsonString());
    }

    [Fact]
    public void ReadTakesDefaultsForMissingMembersAndIgnoresUnknownOnes()
    {
        Assert.Equal(new WindowOpStep(WindowOperation.Minimize), Type.Read(StepJson.Object("{}")));
        Assert.Equal(new WindowOpStep(WindowOperation.Close), Type.Read(StepJson.Object("""{ "operation": "close", "width": 10, "colour": "red" }""")));
    }

    [Theory]
    [InlineData("""{ "operation": "Explode" }""", "'operation'")]
    [InlineData("""{ "operation": 2 }""", "'operation'")]
    [InlineData("""{ "operation": "SetSize", "height": 720 }""", "'width' is required when 'operation' is SetSize.")]
    [InlineData("""{ "operation": "SetSize", "width": 1280 }""", "'height' is required when 'operation' is SetSize.")]
    [InlineData("""{ "operation": "SetSize", "width": 0, "height": 720 }""", "'width' must be between 1 and 32767; got 0.")]
    [InlineData("""{ "operation": "SetSize", "width": 1280, "height": 32768 }""", "'height' must be between 1 and 32767; got 32768.")]
    [InlineData("""{ "operation": "SetSize", "width": "1280", "height": 720 }""", "'width' must be an integer.")]
    [InlineData("""{ "operation": "SetSize", "width": 1280, "height": null }""", "'height' is required when 'operation' is SetSize.")]
    public void BadInputNamesTheMember(string json, string expectedMessageStart)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.StartsWith(expectedMessageStart, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAndExecuteRefuseAForeignStep()
    {
        var foreign = new DelayStep(1);

        Assert.Throws<ArgumentException>(() => Type.Write(foreign));
        Assert.Throws<ArgumentException>(() => Type.Execute(foreign, StepContexts.Create()));
    }
}
