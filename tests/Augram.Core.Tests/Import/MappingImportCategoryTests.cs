using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Xunit;

namespace Augram.Core.Tests.Import;

/// <summary>The add-only merge and categories: merged by name into an existing group, carried whole by a new group.</summary>
public sealed class MappingImportCategoryTests
{
    private static Command Cmd(string name, CommandCategory? category = null)
        => new(CommandId.New(), name, Trigger.ForGesture(GestureId.New()), IsActive: true, [], CategoryId: category?.Id);

    private static CommandCategory Cat(string name) => new(CategoryId.New(), name);

    private static AppGroup GlobalOf(IReadOnlyList<CommandCategory> categories, params Command[] commands)
        => AppGroup.EmptyGlobal with { Commands = commands, Categories = categories };

    private static string? CategoryOf(AppGroup group, string command)
        => group.Commands.Single(candidate => candidate.Name == command).CategoryId is { } id ? group.FindCategory(id)!.Name : null;

    [Fact]
    public void AnAddedCommandJoinsTheExistingCategoryOfTheSameName()
    {
        var mine = Cat("Media");
        var existing = MappingRules.ValidDocument(new([GlobalOf([mine], Cmd("Play", mine))], []));
        var theirs = Cat("MEDIA");

        var result = MappingImport.Merge(existing, new([GlobalOf([theirs], Cmd("Pause", theirs))], []));

        var global = result.Document.Global;
        Assert.Equal(mine, Assert.Single(global.Categories));
        Assert.Equal(mine.Id, global.Commands.Single(command => command.Name == "Pause").CategoryId);
    }

    [Fact]
    public void ACategoryTheGroupLacksArrivesWithItsCommand()
    {
        var existing = MappingRules.ValidDocument(new([GlobalOf([Cat("Media")], Cmd("Play"))], []));
        var window = Cat("Window");

        var result = MappingImport.Merge(existing, new([GlobalOf([window], Cmd("Close", window), Cmd("Loose"))], []));

        var global = result.Document.Global;
        Assert.Equal(["Media", "Window"], global.Categories.Select(category => category.Name));
        Assert.Equal(window.Id, global.FindCategory(window.Id)!.Id);
        Assert.Equal("Window", CategoryOf(global, "Close"));
        Assert.Null(CategoryOf(global, "Loose"));
        Assert.Null(CategoryOf(global, "Play"));
    }

    [Fact]
    public void ACategoryOnlySkippedCommandsUseDoesNotArrive()
    {
        var existing = MappingRules.ValidDocument(new([GlobalOf([], Cmd("Close"))], []));
        var window = Cat("Window");

        var result = MappingImport.Merge(existing, new([GlobalOf([window], Cmd("Close", window))], []));

        Assert.Equal(1, result.CommandsSkipped);
        Assert.Empty(result.Document.Global.Categories);
    }

    [Fact]
    public void AnArrivingCategoryWhoseIdIsTakenGetsAFreshOne()
    {
        var mine = Cat("Media");
        var existing = MappingRules.ValidDocument(new([GlobalOf([mine], Cmd("Play", mine))], []));
        var renamed = mine with { Name = "Audio" };

        var result = MappingImport.Merge(existing, new([GlobalOf([renamed], Cmd("Mute", renamed))], []));

        var global = result.Document.Global;
        Assert.Equal(["Audio", "Media"], global.Categories.Select(category => category.Name));
        Assert.NotEqual(mine.Id, global.Categories[0].Id);
        Assert.Equal("Audio", CategoryOf(global, "Mute"));
        Assert.Equal("Media", CategoryOf(global, "Play"));
    }

    [Fact]
    public void ANewGroupKeepsItsCategoriesWhole()
    {
        var blend = Cat("Blend");
        var general = Cat("General");
        var photo = new AppGroup(GroupId.New(), "Photo", IsActive: true, SuppressGlobals: false, new AppMatcher { ProcessNames = ["photo.exe"] }, [Cmd("Lighten", blend)], [blend, general]);

        var result = MappingImport.Merge(MappingDocument.Empty, new([AppGroup.EmptyGlobal, photo], []));

        var merged = result.Document.Groups.Single(group => group.Name == "Photo");
        Assert.Equal([blend, general], merged.Categories);
        Assert.Equal("Blend", CategoryOf(merged, "Lighten"));
    }
}
