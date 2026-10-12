using Augram.App.Tests.Support;
using Augram.App.UsedBy;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.UsedBy;

/// <summary>The "Used by…" rows, labelled as Core labels a command (<see cref="CommandNames.Label(AppGroup, Command)"/>, its own tests in Core).</summary>
public sealed class UsedByRowTests
{
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
        Assert.Equal(["Global", "Chrome"], rows.Select(row => row.Group));
    }
}
