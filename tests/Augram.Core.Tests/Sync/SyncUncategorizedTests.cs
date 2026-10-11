using System.Text.Json.Nodes;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// Every Global command is in a category (Joel, 2026-10-11): a command left without one goes into "Uncategorized", made when
/// it is missing with a fixed id (<see cref="CategoryRules.UncategorizedId"/>). Categories merge by id, so two machines that
/// each make one end with one, and a file from a build without the rule reads as the same items.
/// </summary>
public sealed class SyncUncategorizedTests : TwoMachineTest
{
    [Fact]
    public void TwoMachinesThatEachMakeUncategorized_EndWithOne()
    {
        var window = NewCategory("Window");
        var media = NewCategory("Media");
        var (work, home) = Shared(NewGlobal(NewCommand("Minimize").In(window), NewCommand("Play").In(media)) with { Categories = [media, window] });
        Assert.Null(CategoryRules.Uncategorized(home.Mapping.Global));

        // Each deletes a different category, so each moves its commands into an Uncategorized of its own.
        DeleteCategory(work, "Window");
        DeleteCategory(home, "Media");
        Assert.Equal(Uncategorized, CategoryRules.Uncategorized(work.Mapping.Global));
        Assert.Equal(Uncategorized, CategoryRules.Uncategorized(home.Mapping.Global));
        var first = work.Sync();
        var second = home.Sync();
        var third = work.Sync();

        Assert.All([first, second, third], report => Assert.Empty(report.Conflicts));
        Assert.All([first, second, third], report => Assert.Empty(report.Repairs));
        Assert.True(work.SameAs(home));
        Assert.Equal([Uncategorized], home.Mapping.Global.Categories);
        Assert.All(home.Mapping.Global.Commands, command => Assert.Equal(Uncategorized.Id, command.CategoryId));
    }

    [Fact]
    public void ARenamedUncategorizedStaysRenamed_AndTheNextOneTwoMachinesMakeIsStillOne()
    {
        var (work, home) = Shared(NewGlobal(NewCommand("Close")));
        var global = work.Mapping.Global;
        work.Mapping.UpdateGroup(global with { Categories = [.. global.Categories.Select(category => category.Id == Uncategorized.Id ? category with { Name = "Misc" } : category)] });
        work.Sync();
        home.Sync();
        Assert.Equal("Misc", Assert.Single(home.Mapping.Global.Categories).Name);

        work.Mapping.AddCommand(GroupId.Global, NewCommand("Paste"));
        home.Mapping.AddCommand(GroupId.Global, NewCommand("Copy"));
        var fresh = CategoryRules.Uncategorized(work.Mapping.Global)!;
        Assert.NotEqual(Uncategorized.Id, fresh.Id);
        Assert.Equal(fresh, CategoryRules.Uncategorized(home.Mapping.Global));
        var first = work.Sync();
        var second = home.Sync();
        var third = work.Sync();

        Assert.All([first, second, third], report => Assert.Empty(report.Conflicts));
        Assert.All([first, second, third], report => Assert.Empty(report.Repairs));
        Assert.True(work.SameAs(home));
        Assert.Equal(["Misc", "Uncategorized"], home.Mapping.Global.Categories.Select(category => category.Name));
        Assert.Equal(Uncategorized.Id, home.Command("Close").CategoryId);
        Assert.Equal(fresh.Id, home.Command("Paste").CategoryId);
        Assert.Equal(fresh.Id, home.Command("Copy").CategoryId);
    }

    [Fact]
    public void AFileFromABuildWithoutTheRule_ReadsWithTheSameUncategorized_SoTheMachinesAgree()
    {
        var (work, home) = Shared(NewGlobal(NewCommand("Close")));

        // An older Augram on the other machine publishes its Global commands without a category, and no Uncategorized.
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Paste"));
        work.Sync();
        WithoutUncategorized(work);
        var report = home.Sync();
        var back = work.Sync();

        Assert.Empty(report.Conflicts);
        Assert.Empty(report.Repairs);
        Assert.Equal(SyncStatus.UpToDate, back.Status);
        Assert.True(work.SameAs(home));
        Assert.Equal([Uncategorized], home.Mapping.Global.Categories);
        Assert.Equal(Uncategorized.Id, home.Command("Paste").CategoryId);
    }

    /// <summary>PC-WORK has <paramref name="global"/> and syncs first; PC-HOME joins it with "use the synced settings".</summary>
    private (SyncMachine Work, SyncMachine Home) Shared(AppGroup global)
    {
        var work = Machine("PC-WORK", ([], Document(global)));
        var home = Machine("PC-HOME", ([], MappingDocument.Empty));
        Assert.Equal(SyncStatus.UpToDate, work.Sync().Status);
        Assert.Equal(SyncStatus.Applied, home.Sync(SyncJoin.UseRemote).Status);
        return (work, home);
    }

    /// <summary>Removes the category as the Global tab does, leaving its commands without one (the rules then move them).</summary>
    private static void DeleteCategory(SyncMachine machine, string name)
    {
        var global = machine.Mapping.Global;
        machine.Mapping.UpdateGroup(global with { Categories = [.. global.Categories.Where(category => category.Name != name)] });
    }

    /// <summary>The machine's published file as a build without the rule writes it: no Uncategorized, no category naming it.</summary>
    private void WithoutUncategorized(SyncMachine machine)
    {
        var key = machine.Id.ToString("D");
        var root = JsonNode.Parse(Remote.Files[key])!.AsObject();
        var id = Uncategorized.Id.ToString();
        foreach (var group in root["mapping"]!["groups"]!.AsArray().Select(node => node!.AsObject()))
        {
            if (group["categories"] is JsonArray categories)
            {
                foreach (var category in categories.Where(node => (string?)node!["id"] == id).ToList())
                {
                    categories.Remove(category);
                }
            }

            foreach (var command in group["commands"]!.AsArray().Select(node => node!.AsObject()).Where(node => (string?)node["category"] == id))
            {
                command.Remove("category");
            }
        }

        Assert.DoesNotContain(id, root.ToJsonString(), StringComparison.Ordinal);
        Remote.Files[key] = root.ToJsonString();
    }
}
