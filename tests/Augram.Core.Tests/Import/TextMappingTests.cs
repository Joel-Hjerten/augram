using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.TypeText;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// SP.net <c>SendKeys</c> / <c>SendString</c> parameters → steps. The dictionaries are synthetic in the real shape:
/// <see cref="MethodParameterReader"/> keeps <c>sendKeysString</c> and <c>characters</c> as the plain strings SP.net wrote.
/// </summary>
public sealed class TextMappingTests
{
    [Fact]
    public void SendKeysParsesItsStringInKeySyntax()
    {
        var result = TextMapping.FromSendKeys(Parameters("sendKeysString", "go{ENTER}"));

        Assert.NotNull(result);
        Assert.Equal([new TypeTextStep("go"), new HotkeyStep(KeyModifiers.None, KeyCode.Enter)], result.Steps);
        Assert.True(result.IsClean);
    }

    [Fact]
    public void SendKeysTextTakesTheMethodAskedFor()
    {
        var result = TextMapping.FromSendKeys(Parameters("sendKeysString", "fov 70"), TypeTextMethod.Keys);

        Assert.Equal([new TypeTextStep("fov 70", TypeTextMethod.Keys)], result!.Steps);
    }

    [Fact]
    public void SendStringIsTypedAsWritten_NoSyntax()
    {
        Assert.Equal(new TypeTextStep("^w{ENTER} 100% ~"), TextMapping.FromSendString(Parameters("characters", "^w{ENTER} 100% ~")));
        Assert.Equal(new TypeTextStep("  spaced  ", TypeTextMethod.Keys), TextMapping.FromSendString(Parameters("characters", "  spaced  "), TypeTextMethod.Keys));
    }

    [Fact]
    public void AMissingOrEmptyParameterGivesNull()
    {
        Assert.Null(TextMapping.FromSendKeys(Parameters("sendKeysString", string.Empty)));
        Assert.Null(TextMapping.FromSendKeys(Parameters("keys", "^w")));
        Assert.Null(TextMapping.FromSendKeys(ImportedStep.NoParameters));
        Assert.Null(TextMapping.FromSendString(Parameters("characters", string.Empty)));
        Assert.Null(TextMapping.FromSendString(Parameters("text", "hello")));
    }

    [Fact]
    public void ASavedPlaceholderUpgradesToTheStepsThatReplaceIt()
    {
        var sendKeys = TextMapping.TryUpgrade(new ImportedStep("SendKeys", "Close tab", Parameters("sendKeysString", "^w")));
        Assert.Equal([new HotkeyStep(KeyModifiers.Control, KeyCode.W)], sendKeys!.Steps);

        var sendString = TextMapping.TryUpgrade(new ImportedStep("SendString", "Greeting", Parameters("characters", "hello")), TypeTextMethod.Keys);
        Assert.Equal([new TypeTextStep("hello", TypeTextMethod.Keys)], sendString!.Steps);
        Assert.True(sendString.IsClean);

        Assert.Null(TextMapping.TryUpgrade(new ImportedStep("SendKeys", "Empty", ImportedStep.NoParameters)));
        Assert.Null(TextMapping.TryUpgrade(new ImportedStep("SendAltDown", "Hold Alt", ImportedStep.NoParameters)));
    }

    [Theory]
    [InlineData("go{ENTER}", true)]
    [InlineData("{LEFT 0}", false)]
    [InlineData("{BREAK}x", false)]
    public void ASendKeysResultIsCompleteWhenCleanAndNotEmpty(string keys, bool complete)
    {
        Assert.Equal(complete, TextMapping.FromSendKeys(Parameters("sendKeysString", keys))!.IsComplete);
    }

    private static Dictionary<string, string> Parameters(string name, string value) => new(StringComparer.Ordinal) { [name] = value };
}
