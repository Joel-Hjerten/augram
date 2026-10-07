using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class StepParametersTests
{
    [Theory]
    [InlineData("Close", WindowOperation.Close)]
    [InlineData("close", WindowOperation.Close)]
    [InlineData("SNAPLEFTHALF", WindowOperation.SnapLeftHalf)]
    public void ReadEnumMatchesNamesIgnoringCase(string text, WindowOperation expected)
    {
        var parameters = StepJson.Object($$"""{ "operation": "{{text}}" }""");

        Assert.Equal(expected, StepParameters.ReadEnum<WindowOperation>(parameters, "operation"));
    }

    [Fact]
    public void ReadEnumIsNullWhenAbsentOrNull()
    {
        Assert.Null(StepParameters.ReadEnum<WindowOperation>(StepJson.Object("{}"), "operation"));
        Assert.Null(StepParameters.ReadEnum<WindowOperation>(StepJson.Object("""{ "operation": null }"""), "operation"));
    }

    [Theory]
    [InlineData("""{ "operation": "Explode" }""", "'operation' must be one of Close, Minimize")]
    [InlineData("""{ "operation": "3" }""", "'operation' must be one of")]
    [InlineData("""{ "operation": 3 }""", "'operation' must be a string.")]
    [InlineData("""{ "operation": {} }""", "'operation' must be a string.")]
    public void ReadEnumRefusesUnknownNamesNumbersAndNonStrings(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => StepParameters.ReadEnum<WindowOperation>(StepJson.Object(json), "operation"));

        Assert.StartsWith(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadInt32ReturnsTheNumberWithinRangeAndNullWhenAbsent()
    {
        Assert.Equal(30, StepParameters.ReadInt32(StepJson.Object("""{ "ms": 30 }"""), "ms", 0, 100));
        Assert.Equal(0, StepParameters.ReadInt32(StepJson.Object("""{ "ms": 0 }"""), "ms", 0, 100));
        Assert.Equal(100, StepParameters.ReadInt32(StepJson.Object("""{ "ms": 100 }"""), "ms", 0, 100));
        Assert.Null(StepParameters.ReadInt32(StepJson.Object("{}"), "ms", 0, 100));
    }

    [Theory]
    [InlineData("""{ "ms": -1 }""", "'ms' must be between 0 and 100; got -1.")]
    [InlineData("""{ "ms": 101 }""", "'ms' must be between 0 and 100; got 101.")]
    [InlineData("""{ "ms": "30" }""", "'ms' must be an integer.")]
    [InlineData("""{ "ms": 1.5 }""", "'ms' must be an integer.")]
    [InlineData("""{ "ms": true }""", "'ms' must be an integer.")]
    [InlineData("""{ "ms": [30] }""", "'ms' must be an integer.")]
    public void ReadInt32RefusesOutOfRangeAndNonIntegers(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => StepParameters.ReadInt32(StepJson.Object(json), "ms", 0, 100));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void ReadStringReturnsTextNullWhenAbsentAndRefusesOtherShapes()
    {
        Assert.Equal("hi", StepParameters.ReadString(StepJson.Object("""{ "text": "hi" }"""), "text"));
        Assert.Equal(string.Empty, StepParameters.ReadString(StepJson.Object("""{ "text": "" }"""), "text"));
        Assert.Null(StepParameters.ReadString(StepJson.Object("{}"), "text"));

        var ex = Assert.Throws<StepFormatException>(() => StepParameters.ReadString(StepJson.Object("""{ "text": 5 }"""), "text"));
        Assert.Equal("'text' must be a string.", ex.Message);
    }

    [Fact]
    public void RequiredNamesTheMemberAndTheCondition()
    {
        var ex = StepParameters.Required("width", "'operation' is SetSize");

        Assert.Equal("'width' is required when 'operation' is SetSize.", ex.Message);
    }

    [Fact]
    public void ExpectReturnsTheStepOfTheRightTypeAndRefusesOthers()
    {
        var delay = new DelayStep(10);

        Assert.Same(delay, StepParameters.Expect<DelayStep>(delay, DelayStepType.Instance));

        var ex = Assert.Throws<ArgumentException>(() => StepParameters.Expect<DelayStep>(new MediaKeyStep(MediaKeyKind.Stop), DelayStepType.Instance));
        Assert.Contains("'delay' step type was handed a 'mediaKey' step", ex.Message, StringComparison.Ordinal);
    }
}
