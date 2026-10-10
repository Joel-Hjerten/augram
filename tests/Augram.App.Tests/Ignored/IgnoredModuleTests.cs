using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>The Ignored tab's composition (plan 0004 step 7): two sub-tabs, Global and Per command, each over its own view model, Per command's "Used by" opening commands through the Commands tab's locator.</summary>
public sealed class IgnoredModuleTests
{
    [AvaloniaFact]
    public void TheTabHasAGlobalAndAPerCommandSubTab_EachListingItsOwnEntries()
    {
        var spine = new IgnoredApp(GroupId.New(), "Spine", IsActive: true, new AppMatcher { WindowsProcessNames = ["Spine.exe"] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
        var zoom = new Command(CommandId.New(), "Zoom in", Trigger.None, IsActive: true, []) { NotIn = [spine.Id] };
        var services = new ServiceCollection();
        services.AddSingleton(new MappingStore(new MappingDocument([AppGroup.EmptyGlobal with { Commands = [zoom] }], [spine, vmware])));
        CommandsModule.Register(services);
        IgnoredModule.Register(services);
        using var provider = services.BuildServiceProvider();

        var entry = IgnoredModule.NavEntry(provider);

        Assert.Equal(AppNavigation.IgnoredKey, entry.Key);
        Assert.Null(entry.Screen);
        Assert.Equal([("Global", AppNavigation.IgnoredGlobalKey), ("Per command", AppNavigation.IgnoredPerCommandKey)], entry.SubEntries!.Select(sub => (sub.Title, sub.Key)));
        Assert.Equal(["VMware"], Build(entry.SubEntries![0]).Items.Select(item => item.Name));
        var perCommand = Build(entry.SubEntries[1]);
        Assert.Equal(["Spine"], perCommand.Items.Select(item => item.Name));
        Assert.Equal("Move to Global", perCommand.MoveLabel);

        var vm = provider.GetRequiredKeyedService<IgnoredViewModel>(IgnoreScope.PerCommand);
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single()));
        var usedBy = vm.Detail!.Sections.SelectMany(section => section.Fields).OfType<LinksField>().Single();
        Assert.NotNull(Assert.Single(usedBy.Links.Get()).Open);
    }

    [AvaloniaFact]
    public void WithoutRegisterEachSubTabSaysSoInsteadOfFailing()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var entry = IgnoredModule.NavEntry(provider);

        Assert.All(entry.SubEntries!, sub => Assert.IsType<TextScreen>(sub.Screen!()));
    }

    private static MasterDetail Build(NavEntry entry) => Assert.IsType<MasterDetail>(Assert.IsType<ComponentScreen>(entry.Screen!()).Build());
}
