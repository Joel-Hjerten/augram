using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>
/// One inline document for the names the importer makes unique, for every kind alike: gestures, apps ("Global" is taken), ignored
/// apps and a group's commands. The first holder keeps a name; a repeat (case-insensitive) takes the first free " (2)", " (3)"…
/// with one warning naming the kind. A blank name is the kind and the entry's position ("App 2"), skipped entries counted.
/// And an entry whose matcher ends up empty reads the same for an app and an ignored app.
/// </summary>
public sealed class DuplicateNameTests
{
    private const string Matcher = "\"FileName\": { \"Value\": \"synthetic.exe\", \"IsRegex\": false }";

    private static readonly ImportResult Result = StrokesPlusImporter.ReadAll(
        "{ \"Gestures\": [ " + Gesture("Synthetic Zig") + ", " + Gesture("synthetic zig") + ", " + Gesture("Synthetic Zig") + " ], "
        + "\"GlobalApplication\": { \"Actions\": [ { \"Description\": \"Synthetic Close\" }, { \"Description\": \"Synthetic Close\" } ] }, "
        + "\"Applications\": [ "
        + "{ \"Description\": \"Global\", " + Matcher + ", \"Actions\": [ { \"Description\": \"Synthetic Close\" } ] }, "
        + "{ \"Description\": \"Synthetic App\", " + Matcher + " }, "
        + "{ \"Description\": \"synthetic app\", " + Matcher + " } ], "
        + "\"IgnoredApplications\": [ "
        + "{ \"Description\": \"Synthetic Ignored\", " + Matcher + " }, "
        + "{ \"Description\": \"Synthetic Ignored\", " + Matcher + " } ] }");

    private static string Gesture(string name)
        => "{ \"Name\": \"" + name + "\", \"PointPatterns\": [ { \"Points\": [ { \"X\": 0, \"Y\": 0 }, { \"X\": 10, \"Y\": 0 }, { \"X\": 10, \"Y\": 10 } ] } ] }";

    private static AppGroup Group(string name) => Result.Mapping.Groups.Single(group => group.Name == name);

    [Fact]
    public void RepeatsAreNumberedFromTwoComparedIgnoringCase()
    {
        Assert.Equal(["Synthetic Zig", "synthetic zig (2)", "Synthetic Zig (3)"], Result.Gestures.Select(gesture => gesture.Name));
        Assert.Equal(["Synthetic Close", "Synthetic Close (2)"], Result.Mapping.Global.Commands.Select(command => command.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Synthetic Close"], Group("Global (2)").Commands.Select(command => command.Name));
        Assert.Equal(
            ["Global", "Global (2)", "Synthetic App", "synthetic app (2)"],
            Result.Mapping.Groups.Select(group => group.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Synthetic Ignored", "Synthetic Ignored (2)"], Result.Mapping.Ignored.Select(app => app.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EachRenameIsOneWarningNamingTheKindInReadingOrder()
    {
        Assert.Equal(
            [
                new ImportWarning(ImportSeverity.Warning, "synthetic zig", "Duplicate gesture name; imported as 'synthetic zig (2)'."),
                new ImportWarning(ImportSeverity.Warning, "Synthetic Zig", "Duplicate gesture name; imported as 'Synthetic Zig (3)'."),
                new ImportWarning(ImportSeverity.Warning, "Synthetic Close", "Duplicate command name; imported as 'Synthetic Close (2)'."),
                new ImportWarning(ImportSeverity.Warning, "Global", "Duplicate app name; imported as 'Global (2)'."),
                new ImportWarning(ImportSeverity.Warning, "synthetic app", "Duplicate app name; imported as 'synthetic app (2)'."),
                new ImportWarning(ImportSeverity.Warning, "Synthetic Ignored", "Duplicate ignored app name; imported as 'Synthetic Ignored (2)'."),
            ],
            Result.Warnings.Where(warning => warning.Message.StartsWith("Duplicate ", StringComparison.Ordinal)));
    }

    [Fact]
    public void ABlankNameIsTheKindAndThePositionForEveryKind()
    {
        var result = StrokesPlusImporter.ReadAll(
            "{ \"Gestures\": [ 5, " + Gesture("  ") + " ], "
            + "\"GlobalApplication\": { \"Actions\": [ 5, { \"Description\": \"  \" } ] }, "
            + "\"Applications\": [ 5, { \"Description\": \"\", " + Matcher + " } ], "
            + "\"IgnoredApplications\": [ 5, { " + Matcher + " } ] }");

        Assert.Equal("Unnamed gesture 2", Assert.Single(result.Gestures).Name);
        Assert.Equal("Action 2", Assert.Single(result.Mapping.Global.Commands).Name);
        Assert.Equal("App 2", result.Mapping.Groups.Single(group => !group.IsGlobal).Name);
        Assert.Equal("Ignored app 2", Assert.Single(result.Mapping.Ignored).Name);
    }

    [Fact]
    public void AnEmptyMatcherImportsInactiveWithTheSameWarningForAppsAndIgnoredApps()
    {
        var result = StrokesPlusImporter.ReadAll(
            "{ \"Applications\": [ { \"Description\": \"Synthetic Blank\", \"Active\": true } ], "
            + "\"IgnoredApplications\": [ { \"Description\": \"Synthetic Blank Ignored\", \"Active\": true } ] }");

        Assert.False(result.Mapping.Groups.Single(group => !group.IsGlobal).IsActive);
        Assert.False(Assert.Single(result.Mapping.Ignored).IsActive);
        Assert.Equal(
            [
                new ImportWarning(ImportSeverity.Warning, "Synthetic Blank", "No usable app definition; imported inactive (needs an app definition)."),
                new ImportWarning(ImportSeverity.Warning, "Synthetic Blank Ignored", "No usable app definition; imported inactive (needs an app definition)."),
            ],
            result.Warnings.Where(warning => warning.Item.StartsWith("Synthetic Blank", StringComparison.Ordinal)));
    }
}
