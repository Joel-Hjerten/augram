using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>SP.net <c>Categories[]</c> and <c>Action.Category</c>: the full fixture's three shapes (Global with several, an app with only "General", an app with two) plus inline edge cases.</summary>
public sealed class CategoryMappingTests
{
    private static readonly ImportResult Full = ReadFixture();

    private static ImportResult ReadFixture()
    {
        using var stream = File.OpenRead(FixturePath.StrokesPlusNet("sample-config-full.json"));
        return StrokesPlusImporter.ReadAll(stream);
    }

    private static AppGroup Group(ImportResult result, string name) => result.Mapping.Groups.Single(group => group.Name == name);

    private static string? CategoryOf(AppGroup group, string command)
        => group.Commands.Single(candidate => candidate.Name == command).CategoryId is { } id ? group.FindCategory(id)!.Name : null;

    private static ImportResult Read(string globalJson)
        => StrokesPlusImporter.ReadAll("{ \"Gestures\": [], \"GlobalApplication\": " + globalJson + " }");

    private static string Action(string name, string? category)
        => "{ \"Description\": \"" + name + "\"" + (category is null ? string.Empty : ", \"Category\": \"" + category + "\"") + ", \"Steps\": [] }";

    [Fact]
    public void GlobalKeepsTheUsedCategoriesInTheListsSpellingSortedByName()
    {
        var global = Full.Mapping.Global;

        Assert.Equal(["Synthetic Media", "Synthetic Programs", "Synthetic Stray", "Synthetic Window"], global.Categories.Select(category => category.Name));
        Assert.Equal("Synthetic Window", CategoryOf(global, "Synthetic Close"));
        Assert.Equal("Synthetic Window", CategoryOf(global, "Synthetic Alt Tab"));
        Assert.Equal("Synthetic Media", CategoryOf(global, "Synthetic Volume Down"));
        Assert.Equal("Synthetic Programs", CategoryOf(global, "Synthetic Explorer"));
    }

    [Fact]
    public void ActionsWithoutACategoryImportUncategorized()
    {
        Assert.Null(CategoryOf(Full.Mapping.Global, "Synthetic Dangling"));
        Assert.Null(CategoryOf(Full.Mapping.Global, "Synthetic Close (2)"));
    }

    [Fact]
    public void AnUnusedListedCategoryIsDroppedWithoutAWarning()
    {
        Assert.DoesNotContain(Full.Mapping.AllCommands(), entry => entry.Group.Categories.Any(category => category.Name == "Synthetic Unused"));
        Assert.DoesNotContain(Full.Warnings, warning => warning.Message.Contains("Synthetic Unused", StringComparison.Ordinal));
    }

    [Fact]
    public void ACategoryMissingFromTheListIsCreatedWithInfo()
    {
        Assert.Equal("Synthetic Stray", CategoryOf(Full.Mapping.Global, "Synthetic Browser Back"));
        var info = Assert.Single(Full.Warnings, warning => warning.Message.Contains("Synthetic Stray", StringComparison.Ordinal));
        Assert.Equal(ImportSeverity.Info, info.Severity);
        Assert.Equal("Global", info.Item);
    }

    [Theory]
    [InlineData("Synthetic Browser")]
    [InlineData("Synthetic Blank")]
    [InlineData("Synthetic Steam Games")]
    public void AnAppWhoseActionsAllSitInGeneralGetsNoCategories(string app)
    {
        var group = Group(Full, app);

        Assert.Empty(group.Categories);
        Assert.All(group.Commands, command => Assert.Null(command.CategoryId));
    }

    [Fact]
    public void GeneralBesideAnotherCategoryIsKept()
    {
        var players = Group(Full, "Synthetic Players");

        Assert.Equal(["General", "Synthetic Transport"], players.Categories.Select(category => category.Name));
        Assert.Equal("General", CategoryOf(players, "Synthetic Mute"));
        Assert.Equal("Synthetic Transport", CategoryOf(players, "Synthetic Next"));
    }

    [Fact]
    public void EveryImportedCategoryHasAFreshDistinctId()
    {
        var ids = Full.Mapping.Groups.SelectMany(group => group.Categories).Select(category => category.Id).ToList();

        Assert.Equal(6, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(default, ids);
    }

    [Fact]
    public void OnlyGeneralIsDroppedInGlobalTooAndMatchesIgnoringCase()
    {
        var result = Read("{ \"Categories\": [ \"General\" ], \"Actions\": [ " + Action("Synthetic A", "general") + ", " + Action("Synthetic B", null) + " ] }");

        Assert.Empty(result.Mapping.Global.Categories);
        Assert.All(result.Mapping.Global.Commands, command => Assert.Null(command.CategoryId));
        Assert.DoesNotContain(result.Warnings, warning => warning.Severity == ImportSeverity.Info);
    }

    [Fact]
    public void WithoutACategoryListEachNamedCategoryIsCreatedOnceWithInfo()
    {
        var result = Read("{ \"Actions\": [ " + Action("Synthetic A", "Synthetic Keys") + ", " + Action("Synthetic B", " synthetic keys ") + ", " + Action("Synthetic C", "General") + " ] }");

        var global = result.Mapping.Global;
        Assert.Equal(["General", "Synthetic Keys"], global.Categories.Select(category => category.Name));
        Assert.Equal("Synthetic Keys", CategoryOf(global, "Synthetic B"));
        Assert.Equal(2, result.Warnings.Count(warning => warning.Severity == ImportSeverity.Info && warning.Item == "Global"));
    }

    [Fact]
    public void NoCategoriesAnywhereImportsNone()
    {
        var result = Read("{ \"Categories\": [ \"Synthetic Listed\" ], \"Actions\": [ " + Action("Synthetic A", null) + " ] }");

        Assert.Empty(result.Mapping.Global.Categories);
        Assert.Null(Assert.Single(result.Mapping.Global.Commands).CategoryId);
    }
}
