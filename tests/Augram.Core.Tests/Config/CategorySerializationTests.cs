using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Tests.Mapping.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Config;

/// <summary>A group's <c>categories</c> and a command's <c>category</c> in the file: written only when there is something to write, read leniently.</summary>
public sealed class CategorySerializationTests
{
    private const string GlobalId = "00000000-0000-4000-8000-000000000001";
    private const string MediaId = "6d1e7f3a-0000-4000-8000-0000000000c1";

    private readonly List<string> _notices = [];

    [Fact]
    public void RoundTripKeepsCategoriesAndEachCommandsCategory()
    {
        var document = new ConfigDocument { Mapping = Categorised() };

        var json = ConfigSerializer.Write(document);
        var back = ConfigSerializer.Read(json, FakeStepType.Registry, _notices.Add);

        Assert.Empty(_notices);
        foreach (var (group, backGroup) in document.Mapping.Groups.Zip(back.Mapping.Groups))
        {
            Assert.Equal(group.Categories, backGroup.Categories);
            Assert.Equal(group.Commands.Select(command => command.CategoryId), backGroup.Commands.Select(command => command.CategoryId));
        }

        Assert.Equal(json, ConfigSerializer.Write(back));
    }

    [Fact]
    public void CategoriesAreWrittenAfterTheMatcherAndOmittedWhenEmpty()
    {
        var mapping = Categorised();
        var media = mapping.Global.Categories.Single(category => category.Name == "Media");
        var json = ConfigSerializer.Write(new ConfigDocument { Mapping = mapping }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains($"\"matcher\": null,\n        \"categories\": [\n          {{\n            \"id\": \"{media.Id}\",\n            \"name\": \"Media\"\n          }},", json, StringComparison.Ordinal);
        Assert.Contains($"\"isActive\": true,\n            \"category\": \"{media.Id}\",\n            \"steps\": [", json, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(json, "\"categories\""));
        Assert.Equal(2, Occurrences(json, "\"category\""));
    }

    [Fact]
    public void MissingMembersReadAsNoCategoriesAndUncategorized()
    {
        var back = ConfigSerializer.Read($$"""
            { "schemaVersion": 1, "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global",
              "commands": [ { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Close" } ] } ] } }
            """);

        Assert.Empty(back.Mapping.Global.Categories);
        Assert.Null(Assert.Single(back.Mapping.Global.Commands).CategoryId);
    }

    [Fact]
    public void ACategoryWithoutAUsableIdOrNameIsDroppedWithANoticeAndTheRestLoads()
    {
        var back = Read("""
            [ { "id": "MEDIA", "name": "Media" }, "Window", { "name": "No id" }, { "id": "6d1e7f3a-0000-4000-8000-0000000000c2", "name": "  " },
              { "id": "6d1e7f3a-0000-4000-8000-0000000000c3", "name": "Kept" } ]
            """, "\"category\": \"6d1e7f3a-0000-4000-8000-0000000000c2\"");

        Assert.Equal("Kept", Assert.Single(back.Global.Categories).Name);
        Assert.Equal(
            [
                "Category 1 of app group 'Global' dropped: 'id' is missing or not a Guid string.",
                "Category 2 of app group 'Global' dropped: it is not a JSON object.",
                "Category 3 of app group 'Global' dropped: 'id' is missing or not a Guid string.",
                "Category 4 of app group 'Global' dropped: it has no name.",
            ],
            _notices);
        Assert.Null(new MappingStore(back).Global.Commands[0].CategoryId);
    }

    [Fact]
    public void ACategoryReferenceToNoCategoryOfTheGroupIsClearedSilently()
    {
        var back = Read($$"""[ { "id": "{{MediaId}}", "name": "Media" } ]""", "\"category\": \"6d1e7f3a-0000-4000-8000-0000000000ff\"");

        Assert.Empty(_notices);
        Assert.Null(new MappingStore(back).Global.Commands[0].CategoryId);
    }

    [Theory]
    [InlineData("\"category\": 7")]
    [InlineData("\"category\": \"media\"")]
    public void ACategoryReferenceThatIsNotAGuidReadsAsUncategorizedWithANotice(string member)
    {
        var back = Read($$"""[ { "id": "{{MediaId}}", "name": "Media" } ]""", member);

        Assert.Null(back.Global.Commands[0].CategoryId);
        Assert.Equal(["The category of command 'Close' in 'Global' dropped: 'category' must be a Guid string; the command is uncategorized."], _notices);
    }

    [Fact]
    public void ACategoryReferenceToAGroupCategoryIsKept()
    {
        var back = Read($$"""[ { "id": "{{MediaId}}", "name": " Media " } ]""", $"\"category\": \"{MediaId}\"");

        var global = new MappingStore(back).Global;
        Assert.Equal("Media", Assert.Single(global.Categories).Name);
        Assert.Equal(new CategoryId(new Guid(MediaId)), global.Commands[0].CategoryId);
    }

    private MappingDocument Read(string categories, string commandMember)
        => ConfigSerializer.Read(
            $$"""
            { "schemaVersion": 1, "mapping": { "groups": [ { "id": "{{GlobalId}}", "name": "Global", "categories": {{categories}},
              "commands": [ { "id": "6d1e7f3a-0000-4000-8000-000000000002", "name": "Close", {{commandMember}} } ] } ] } }
            """,
            FakeStepType.Registry,
            _notices.Add).Mapping;

    /// <summary>Global with two categories and one command in each plus one Uncategorized; Chrome with none.</summary>
    private static MappingDocument Categorised()
    {
        var media = NewCategory("Media");
        var window = NewCategory("Window");
        return MappingRules.ValidDocument(Document(
            NewGlobal(NewCommand("Play", Up).In(media), NewCommand("Close", Down).In(window), NewCommand("Later")) with { Categories = [window, media] },
            NewGroup("Chrome", commands: [NewCommand("Close tab", Up)])));
    }

    private static int Occurrences(string text, string part)
        => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
