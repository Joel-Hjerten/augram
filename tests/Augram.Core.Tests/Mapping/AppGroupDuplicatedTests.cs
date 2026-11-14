using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.Core.Tests.Mapping;

/// <summary>A duplicated app group (Joel, 2026-11-14): fresh ids throughout, the commands following their category and hold remap, the executable cleared.</summary>
public sealed class AppGroupDuplicatedTests
{
    [Fact]
    public void DuplicatedTakesCommandsCategoriesAndHoldRemapsWithFreshIds_AndClearsTheExecutable()
    {
        var category = new CommandCategory(CategoryId.New(), "Tabs");
        var space = HoldRemap.For(KeyCode.Space);
        var matcher = new AppMatcher
        {
            WindowsProcessNames = ["chrome.exe"],
            MacProcessNames = ["Google Chrome"],
            ProcessPath = @"C:\Chrome\chrome.exe",
            MacProcessPath = "/Applications/Google Chrome.app",
            Title = "Chrome",
            TitleIsRegex = true,
        };
        var group = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: true, matcher,
            [
                new Command(CommandId.New(), "Close tab", Trigger.None, IsActive: true, []) with { CategoryId = category.Id },
                new Command(CommandId.New(), "Orbit", Trigger.None, IsActive: true, []) with { HoldRemapId = space.Id },
            ],
            [category])
        { HoldRemaps = [space], UseOn = PlatformSet.Windows };

        var copy = group.Duplicated(GroupId.New(), "Chrome (2)");

        Assert.NotEqual(group.Id, copy.Id);
        Assert.Equal("Chrome (2)", copy.Name);
        Assert.True(copy is { IsActive: true, SuppressGlobals: true });
        Assert.Equal(group.UseOn, copy.UseOn);
        Assert.Empty(copy.Matcher!.WindowsProcessNames);
        Assert.Empty(copy.Matcher.MacProcessNames);
        Assert.Null(copy.Matcher.ProcessPath);
        Assert.Null(copy.Matcher.MacProcessPath);
        Assert.Equal(("Chrome", true), (copy.Matcher.Title, copy.Matcher.TitleIsRegex));

        var newCategory = Assert.Single(copy.Categories);
        var newSpace = Assert.Single(copy.HoldRemaps);
        Assert.NotEqual(category.Id, newCategory.Id);
        Assert.NotEqual(space.Id, newSpace.Id);
        Assert.Equal((space.Name, space.HoldKey), (newSpace.Name, newSpace.HoldKey));
        Assert.Equal(["Close tab", "Orbit"], copy.Commands.Select(command => command.Name));
        Assert.DoesNotContain(copy.Commands, command => group.Commands.Any(original => original.Id == command.Id));
        Assert.Equal(newCategory.Id, copy.Commands[0].CategoryId);
        Assert.Equal(newSpace.Id, copy.Commands[1].HoldRemapId);
        Assert.Equal(group, group.Duplicated(group.Id, group.Name) with { Matcher = matcher, Commands = group.Commands, Categories = group.Categories, HoldRemaps = group.HoldRemaps });
    }

    [Fact]
    public void GlobalCannotBeDuplicated()
        => Assert.Throws<MappingValidationException>(() => AppGroup.EmptyGlobal.Duplicated(GroupId.New(), "Global (2)"));
}
