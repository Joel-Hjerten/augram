using Augram.App.Tests.Support;
using Augram.App.UsedBy;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.UsedBy;

/// <summary>The "Used by…" label names the section a command sits in on the Commands tab: Global › category › command, Global › command, app group › command.</summary>
public sealed class UsedByRowTests
{
    [Fact]
    public void AGlobalCommandShowsItsCategoryAndAnAppCommandOnlyItsGroup()
    {
        var media = new CommandCategory(CategoryId.New(), "Media");
        var global = AppGroup.EmptyGlobal with { Categories = [media] };
        var general = new CommandCategory(CategoryId.New(), "General");
        var photoshop = MappingFixture.Group("Photoshop") with { Categories = [general] };

        Assert.Equal("Global › Media › Volume up", UsedByRow.From(global, Command("Volume up") with { CategoryId = media.Id }).Label);
        Assert.Equal("Global › Minimize", UsedByRow.From(global, Command("Minimize")).Label);
        Assert.Equal("Global › Gone", UsedByRow.From(global, Command("Gone") with { CategoryId = CategoryId.New() }).Label);
        Assert.Equal("Chrome › Close tab", UsedByRow.From(MappingFixture.Group("Chrome"), Command("Close tab")).Label);
        Assert.Equal("Photoshop › Brush", UsedByRow.From(photoshop, Command("Brush") with { CategoryId = general.Id }).Label);
    }

    [Fact]
    public void ForListsTheStoresCommandsWithTheirSections()
    {
        var gesture = GestureId.New();
        var media = new CommandCategory(CategoryId.New(), "Media");
        var mapping = new MappingStore();
        mapping.UpdateGroup(mapping.Global with { Categories = [media] });
        mapping.AddCommand(GroupId.Global, MappingFixture.Command("Volume up", gesture) with { CategoryId = media.Id });
        mapping.AddGroup(MappingFixture.Group("Chrome", MappingFixture.Command("Close tab", gesture)));

        var rows = UsedByRow.For(mapping, gesture);

        Assert.Equal(["Global › Media › Volume up", "Chrome › Close tab"], rows.Select(row => row.Label));
        Assert.Equal(["Media", null], rows.Select(row => row.Category));
        Assert.Equal(["Global", "Chrome"], rows.Select(row => row.Group));
    }

    private static Command Command(string name) => MappingFixture.Unbound(name);
}
