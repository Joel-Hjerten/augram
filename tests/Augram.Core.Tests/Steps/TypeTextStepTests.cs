using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.TypeText;
using Augram.Core.Tests.Steps.Support;
using Xunit;

namespace Augram.Core.Tests.Steps;

public sealed class TypeTextStepTests
{
    private static readonly TypeTextStepType Type = TypeTextStepType.Instance;

    [Fact]
    public void MetadataAndTheEmptyDefault()
    {
        Assert.Equal("typeText", Type.Key);
        Assert.Equal("Type text", Type.DisplayName);
        Assert.Equal(StepCategory.Text, Type.Category);
        Assert.True(Type.IsPlatformNeutral);

        var step = Assert.IsType<TypeTextStep>(Type.CreateDefault());
        Assert.Same(TypeTextStep.Empty, step);
        Assert.Equal(string.Empty, step.Text);
        Assert.Equal(TypeTextMethod.Unicode, step.Method);
        Assert.False(step.HasText);
        Assert.Equal("Type text (no text set)", step.Summary);
        Assert.Same(Type, step.Type);
    }

    [Theory]
    [InlineData("fov 67.5", TypeTextMethod.Unicode, "Type \"fov 67.5\"")]
    [InlineData("`", TypeTextMethod.Keys, "Type \"`\" (by keys)")]
    [InlineData("line one\nline two", TypeTextMethod.Unicode, "Type \"line one⏎line two\"")]
    [InlineData("a\r\nb\rc\n", TypeTextMethod.Unicode, "Type \"a⏎b⏎c⏎\"")]
    [InlineData("\n", TypeTextMethod.Keys, "Type \"⏎\" (by keys)")]
    [InlineData("", TypeTextMethod.Keys, "Type text (no text set)")]
    [InlineData("1234567890123456789012345678901234567890", TypeTextMethod.Unicode, "Type \"1234567890123456789012345678901234567890\"")]
    [InlineData("12345678901234567890123456789012345678901", TypeTextMethod.Unicode, "Type \"123456789012345678901234567890123456789…\"")]
    [InlineData("12345678901234567890123456789012345678\U0001F600x", TypeTextMethod.Unicode, "Type \"12345678901234567890123456789012345678…\"")]
    public void SummaryShowsTheTextOnOneLine_ShortenedWithAnEllipsis(string text, TypeTextMethod method, string expected)
    {
        Assert.Equal(expected, new TypeTextStep(text, method).Summary);
    }

    [Theory]
    [InlineData("19800101-1234", TypeTextMethod.Unicode, "Type text (13 characters)")]
    [InlineData("fov 70", TypeTextMethod.Keys, "Type text (6 characters) (by keys)")]
    [InlineData("", TypeTextMethod.Unicode, "Type text (no text set)")]
    public void LogSummaryGivesTheLengthNeverTheText(string text, TypeTextMethod method, string expected)
    {
        IStep step = new TypeTextStep(text, method);

        Assert.Equal(expected, step.LogSummary);
    }

    [Fact]
    public void LinesSplitAtEveryKindOfLineBreak_CrLfOnce()
    {
        Assert.Equal(["a", "b", "c", "", "d"], new TypeTextStep("a\r\nb\rc\n\nd").Lines());
        Assert.Equal([string.Empty], TypeTextStep.Empty.Lines());
        Assert.Equal(["", ""], new TypeTextStep("\r\n").Lines());
    }

    [Theory]
    [InlineData("fov 67.5", TypeTextMethod.Unicode, """{"text":"fov 67.5","method":"Unicode"}""")]
    [InlineData("hello", TypeTextMethod.Keys, """{"text":"hello","method":"Keys"}""")]
    [InlineData("", TypeTextMethod.Unicode, """{"text":"","method":"Unicode"}""")]
    [InlineData("two\nlines", TypeTextMethod.Keys, """{"text":"two\nlines","method":"Keys"}""")]
    public void WriteEmitsBothMembersAndTheRoundTripIsByteStable(string text, TypeTextMethod method, string expectedJson)
    {
        var step = new TypeTextStep(text, method);

        var written = Type.Write(step);

        Assert.Equal(expectedJson, written.ToJsonString());
        var reread = Type.Read(written);
        Assert.Equal(step, reread);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Theory]
    [InlineData("say \"hi\" & <go>")]
    [InlineData("åäö € 日本 \U0001F600")]
    [InlineData("tab\there\r\nwindows\rold mac")]
    [InlineData("  padded  ")]
    public void AnyTextRoundTripsExactly(string text)
    {
        var step = new TypeTextStep(text, TypeTextMethod.Unicode);

        var written = Type.Write(step);
        var reread = Assert.IsType<TypeTextStep>(Type.Read(StepJson.Object(written.ToJsonString())));

        Assert.Equal(text, reread.Text);
        Assert.Equal(written.ToJsonString(), Type.Write(reread).ToJsonString());
    }

    [Fact]
    public void ReadDefaultsMissingMembersAndMatchesTheMethodIgnoringCase()
    {
        Assert.Equal(TypeTextStep.Empty, Type.Read(StepJson.Object("{}")));
        Assert.Equal(TypeTextStep.Empty, Type.Read(StepJson.Object("""{ "text": null, "method": null, "delay": 5 }""")));
        Assert.Equal(new TypeTextStep("hi", TypeTextMethod.Unicode), Type.Read(StepJson.Object("""{ "text": "hi" }""")));
        Assert.Equal(new TypeTextStep(string.Empty, TypeTextMethod.Keys), Type.Read(StepJson.Object("""{ "method": "keys" }""")));
        Assert.Equal(new TypeTextStep("x", TypeTextMethod.Keys), Type.Read(StepJson.Object("""{ "text": "x", "method": "KEYS" }""")));
        Assert.Equal(new TypeTextStep("x", TypeTextMethod.Unicode), Type.Read(StepJson.Object("""{ "text": "x", "method": "unicode" }""")));
    }

    [Theory]
    [InlineData("""{ "text": 5 }""", "'text' must be a string.")]
    [InlineData("""{ "text": ["a"] }""", "'text' must be a string.")]
    [InlineData("""{ "text": true }""", "'text' must be a string.")]
    [InlineData("""{ "text": "a", "method": 1 }""", "'method' must be a string.")]
    [InlineData("""{ "text": "a", "method": "1" }""", "'method' must be one of Unicode, Keys; got '1'.")]
    [InlineData("""{ "text": "a", "method": "Typing" }""", "'method' must be one of Unicode, Keys; got 'Typing'.")]
    public void BadInputNamesTheMember(string json, string expectedMessage)
    {
        var ex = Assert.Throws<StepFormatException>(() => Type.Read(StepJson.Object(json)));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void WriteAndExecuteRefuseAForeignStep()
    {
        Assert.Throws<ArgumentException>(() => Type.Write(new DelayStep(1)));
        Assert.Throws<ArgumentException>(() => Type.Execute(new DelayStep(1), StepContexts.Create()));
    }

    [Theory]
    [InlineData(HostPlatform.Windows, HostPlatform.MacOS)]
    [InlineData(HostPlatform.MacOS, HostPlatform.Windows)]
    public void ConversionLeavesTheStepUnchanged(HostPlatform from, HostPlatform to)
    {
        var step = new TypeTextStep("fov 70", TypeTextMethod.Keys);

        var conversion = Type.Convert(step, from, to);

        Assert.Equal(StepConversionKind.Unchanged, conversion.Kind);
        Assert.Same(step, conversion.Step);
    }

    [Theory]
    [InlineData(TypeTextMethod.Unicode, "text fov 67.5")]
    [InlineData(TypeTextMethod.Keys, "keys fov 67.5")]
    public void ExecuteTypesTheTextByItsMethod(TypeTextMethod method, string expectedCall)
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new TypeTextStep("fov 67.5", method), StepContexts.Create(input: input));

        Assert.Same(StepResult.Done, result);
        Assert.Equal([expectedCall], input.All);
    }

    [Theory]
    [InlineData(TypeTextMethod.Unicode, "text")]
    [InlineData(TypeTextMethod.Keys, "keys")]
    public void LineBreaksArePressedAsEnterBetweenTheLines(TypeTextMethod method, string kind)
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new TypeTextStep("one\r\ntwo\n\nthree\n", method), StepContexts.Create(input: input));

        Assert.Same(StepResult.Done, result);
        Assert.Equal(
            [$"{kind} one", "hotkey None+Enter", $"{kind} two", "hotkey None+Enter", "hotkey None+Enter", $"{kind} three", "hotkey None+Enter"],
            input.All);
    }

    [Fact]
    public void AnEmptyTextSkipsWithoutTouchingTheSimulator()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new TypeTextStep(string.Empty, TypeTextMethod.Keys), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("no text set", result.Reason);
        Assert.Empty(input.All);
    }

    [Fact]
    public void ByKeys_ACharacterAUsKeyboardLacksFailsBeforeAnythingIsTyped_WithoutQuotingIt()
    {
        var input = new FakeInputSimulator();

        var result = Type.Execute(new TypeTextStep("ok\nsmörgås", TypeTextMethod.Keys), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("typing by keys: character 6 has no key on a US layout; Unicode types any character", result.Reason);
        Assert.DoesNotContain("ö", result.Reason, StringComparison.Ordinal);
        Assert.Empty(input.All);

        Assert.Same(StepResult.Done, Type.Execute(new TypeTextStep("ok\nsmörgås", TypeTextMethod.Unicode), StepContexts.Create(input: input)));
    }

    [Theory]
    [InlineData(TypeTextMethod.Unicode, SimulationResult.Failed, "Unicode typing: Failed")]
    [InlineData(TypeTextMethod.Unicode, SimulationResult.Unsupported, "Unicode typing: Unsupported")]
    [InlineData(TypeTextMethod.Keys, SimulationResult.Failed, "typing by keys: Failed")]
    [InlineData(TypeTextMethod.Keys, SimulationResult.Unsupported, "typing by keys: Unsupported")]
    public void ASimulatorRefusalFailsTheStepAndStopsTyping(TypeTextMethod method, SimulationResult answer, string expectedReason)
    {
        var input = new FakeInputSimulator { OtherResult = answer };

        var result = Type.Execute(new TypeTextStep("first\nsecond", method), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Single(input.All);
    }

    [Fact]
    public void AFailedEnterBetweenLinesFailsTheStep()
    {
        var input = new FakeInputSimulator { OtherResult = SimulationResult.Failed };

        var result = Type.Execute(new TypeTextStep("\nsecond"), StepContexts.Create(input: input));

        Assert.Equal(StepOutcome.Failed, result.Outcome);
        Assert.Equal("Enter between lines: Failed", result.Reason);
        Assert.Equal(["hotkey None+Enter"], input.All);
    }

    [Fact]
    public void AnAlreadyCancelledCommandSkipsWithoutTyping()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var input = new FakeInputSimulator();

        var result = Type.Execute(new TypeTextStep("hello"), StepContexts.Create(input: input, cancellation: cts.Token));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled", result.Reason);
        Assert.Empty(input.All);
    }

    [Fact]
    public void CancellationIsHonouredBetweenLines()
    {
        using var cts = new CancellationTokenSource();
        var input = new FakeInputSimulator { OnInjection = entry => CancelOnText(entry, cts) };

        var result = Type.Execute(new TypeTextStep("one\ntwo"), StepContexts.Create(input: input, cancellation: cts.Token));

        Assert.Equal(StepOutcome.Skipped, result.Outcome);
        Assert.Equal("cancelled", result.Reason);
        Assert.Equal(["text one"], input.All);
    }

    [Fact]
    public void TheLogLineCarriesTheLengthNeverTheText()
    {
        var log = new ListEventLog();
        const string secret = "hunter2 is my password";

        Type.Execute(new TypeTextStep(secret + "\nsecond line", TypeTextMethod.Keys), StepContexts.Create(log: log));

        var line = Assert.Single(log.Events);
        Assert.Equal((EventLevel.Debug, "steps", "Type text"), (line.Level, line.Source, line.Message));
        Assert.Contains(new LogProperty("method", TypeTextMethod.Keys), line.Properties!);
        Assert.Contains(new LogProperty("length", secret.Length + 12), line.Properties!);
        Assert.Contains(new LogProperty("lines", 2), line.Properties!);
        Assert.Contains(new LogProperty("outcome", StepOutcome.Done), line.Properties!);
        Assert.DoesNotContain(line.Properties!, property => property.Value?.ToString()?.Contains("hunter2", StringComparison.Ordinal) == true);
    }

    /// <summary>Cancels the command as the first text is typed, as a stop arriving mid-step would.</summary>
    private static void CancelOnText(string entry, CancellationTokenSource cancellation)
    {
        if (entry.StartsWith("text ", StringComparison.Ordinal))
        {
            cancellation.Cancel();
        }
    }
}
