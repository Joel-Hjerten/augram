using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>Inline documents for the matcher rows: regex alternations, title preference, class chains, invalid patterns, naming.</summary>
public sealed class MatcherMappingTests
{
    private static ImportResult Read(string applicationMembers)
        => StrokesPlusImporter.ReadAll("{ \"Applications\": [ { " + applicationMembers + " } ] }");

    private static AppGroup App(ImportResult result) => result.Mapping.Groups.Single(group => !group.IsGlobal);

    private static AppMatcher Matcher(string applicationMembers) => App(Read(applicationMembers)).Matcher!;

    private static string Field(string name, string value, bool isRegex)
        => "\"" + name + "\": { \"Value\": \"" + value + "\", \"IsRegex\": " + (isRegex ? "true" : "false") + " }";

    [Theory]
    [InlineData("a\\\\.exe|b\\\\.exe")]
    [InlineData("(a\\\\.exe|b\\\\.exe)")]
    [InlineData("(?:a\\\\.exe|b\\\\.exe)")]
    [InlineData("^a\\\\.exe|b\\\\.exe$")]
    [InlineData(" a\\\\.exe | b\\\\.exe ")]
    public void PlainAlternationSplitsIntoProcessNames(string pattern)
    {
        Assert.Equal(["a.exe", "b.exe"], Matcher("\"Description\": \"Synthetic App\", " + Field("FileName", pattern, true)).WindowsProcessNames);
    }

    [Fact]
    public void SingleLiteralRegexBecomesOneName()
    {
        Assert.Equal(["chrome.exe"], Matcher("\"Description\": \"Synthetic App\", " + Field("FileName", "chrome\\\\.exe", true)).WindowsProcessNames);
    }

    [Theory]
    [InlineData("PotPlayerMini.*\\\\.exe")]
    [InlineData("Spine(?:-1)?\\\\.exe")]
    [InlineData("a\\\\d\\\\.exe")]
    [InlineData("(a|b)|c")]
    [InlineData("a|")]
    public void OtherPatternsLeaveProcessNamesEmptyWithWarning(string pattern)
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("FileName", pattern, true) + ", " + Field("FilePath", "C:\\\\\\\\Apps", false));

        Assert.Empty(App(result).Matcher!.WindowsProcessNames);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic App" && warning.Message.Contains("not a plain list of names", StringComparison.Ordinal));
        Assert.True(App(result).IsActive);
    }

    [Fact]
    public void OwnerTitleIsUsedWhenRootIsEmpty()
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("RootWindowText", "", false) + ", " + Field("OwnerWindowText", "Chimera", false));

        Assert.Equal("Chimera", App(result).Matcher!.Title);
        Assert.False(App(result).Matcher!.TitleIsRegex);
        Assert.DoesNotContain(result.Warnings, warning => warning.Message.Contains("is used as the title", StringComparison.Ordinal));
    }

    [Fact]
    public void SameTextInSeveralFieldsDoesNotWarn()
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("RootWindowText", "Same", false) + ", " + Field("ParentWindowText", "Same", false));

        Assert.Equal("Same", App(result).Matcher!.Title);
        Assert.DoesNotContain(result.Warnings, warning => warning.Message.Contains("differ", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassNameRegexThatIsNotPlainIsSkippedWithWarning()
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("FileName", "x.exe", false) + ", " + Field("OwnerClassName", "Progman.*", true) + ", " + Field("ControlClassName", "SysListView32", false));

        Assert.Equal(["SysListView32"], App(result).Matcher!.ClassChain);
        Assert.Contains(result.Warnings, warning => warning.Message.Contains("OwnerClassName pattern 'Progman.*' is not a plain list of class names", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactPathIsKeptWithoutRegex()
    {
        var matcher = Matcher("\"Description\": \"Synthetic App\", " + Field("FilePath", "C:\\\\\\\\Apps\\\\\\\\x.exe", false));

        Assert.Equal("C:\\\\Apps\\\\x.exe", matcher.ProcessPath);
        Assert.False(matcher.ProcessPathIsRegex);
    }

    [Fact]
    public void InvalidPathRegexIsDroppedWithWarningAndTheGroupSurvives()
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("FileName", "x.exe", false) + ", " + Field("FilePath", "(", true));

        Assert.Null(App(result).Matcher!.ProcessPath);
        Assert.Equal(["x.exe"], App(result).Matcher!.WindowsProcessNames);
        Assert.True(App(result).IsActive);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic App" && warning.Message.Contains("not a valid regular expression", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void UnsetControlIdDoesNotWarn(string value)
    {
        var result = Read("\"Description\": \"Synthetic App\", " + Field("FileName", "x.exe", false) + ", \"ControlID\": " + value);

        Assert.DoesNotContain(result.Warnings, warning => warning.Message.Contains("ControlID", StringComparison.Ordinal));
    }

    [Fact]
    public void AppNamedGlobalIsRenamed()
    {
        var result = Read("\"Description\": \"Global\", " + Field("FileName", "x.exe", false));

        Assert.Equal("Global (2)", App(result).Name);
        Assert.Contains(result.Warnings, warning => warning.Item == "Global" && warning.Message.Contains("Global (2)", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingDescriptionFallsBackToANumberedName()
    {
        Assert.Equal("App 1", App(Read(Field("FileName", "x.exe", false))).Name);
    }

    [Fact]
    public void IgnoredAppWithEmptyMatcherImportsInactive()
    {
        var result = StrokesPlusImporter.ReadAll("{ \"IgnoredApplications\": [ { \"Description\": \"Synthetic Ignored\", \"Active\": true, \"DisableOnFocus\": true } ] }");

        var app = Assert.Single(result.Mapping.Ignored);
        Assert.False(app.IsActive);
        Assert.True(app.DisableEntirely);
        Assert.Contains(result.Warnings, warning => warning.Item == "Synthetic Ignored" && warning.Message.Contains("needs an app definition", StringComparison.Ordinal));
    }
}
