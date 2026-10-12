using Augram.App.Components.CommandTree;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The search on a Commands tab (plan 0009): the view model narrows its sections and the selection survives a search that hides it (decision 5).</summary>
public sealed class CommandsViewModelSearchTests
{
    [AvaloniaFact]
    public void TheSelectionSurvivesASearchThatHidesIt_AndIsBackExpandedAfterClearing()
    {
        var (vm, _, _, _) = Create();
        var chrome = Section(vm, "Chrome");
        var closeTab = Item(vm, "Close tab");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, chrome, closeTab));

        vm.SearchText = "brush";

        Assert.Equal(["Photoshop"], Names(vm));
        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.Equal(chrome.Id, vm.SelectedSectionId);
        Assert.Equal("Close tab", vm.SelectedCommand!.Name);
        Assert.Equal("Wait 5 ms", Assert.Single(vm.Steps).Summary);

        vm.SearchText = string.Empty;

        Assert.Equal(["Apple", "Chrome", "Photoshop"], Names(vm));
        Assert.Equal(closeTab.Id, vm.SelectedCommandId);
        Assert.True(Section(vm, "Chrome").IsExpanded);
        Assert.Contains(Section(vm, "Chrome").Commands, command => command.Id == closeTab.Id);
    }

    [AvaloniaFact]
    public void WhileSearchingAHeaderToggleLeavesTheSavedExpandedSetAlone()
    {
        var (vm, _, _, _) = Create();
        vm.SearchText = "chrome";
        Assert.True(Section(vm, "Chrome").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Chrome")));
        Assert.True(Section(vm, "Chrome").IsExpanded);

        vm.SearchText = string.Empty;
        Assert.False(Section(vm, "Chrome").IsExpanded);
    }
}
