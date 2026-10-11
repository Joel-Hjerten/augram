using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// Show other platforms blinks when an edit hides the selected row behind it (Joel, 0.11.9: "make it blink twice so the user
/// understands they can make it visible again"): the view model counts such edits, the tree blinks its toggle at each.
/// </summary>
public sealed class CommandsViewModelPlatformNudgeTests
{
    [AvaloniaFact]
    public void UntickingThisPlatformOnTheSelectedCommandNudgesTheFilter()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global, platform: HostPlatform.Windows);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window"), Item(vm, "Minimize")));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, Section(vm, "Window"), Item(vm, "Minimize"), useOn: PlatformSet.MacOS));

        Assert.Null(vm.SelectedCommandId);
        Assert.Equal(1, vm.PlatformFilterNudges);
    }

    [AvaloniaFact]
    public void UntickingThisPlatformOnTheSelectedCategoryNudgesTheFilter()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.Windows);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window")));

        store.UpdateGroup(store.Global with { Categories = [.. store.Global.Categories.Select(category => category.Name == "Window" ? category with { UseOn = PlatformSet.MacOS } : category)] });

        Assert.Null(vm.SelectedSectionId);
        Assert.Equal(1, vm.PlatformFilterNudges);
    }

    [AvaloniaFact]
    public void NoNudgeWhileOtherPlatformsShow_NorWhenTheUserTurnsTheFilterOff()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global, platform: HostPlatform.Windows);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window"), Item(vm, "Minimize")));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, Section(vm, "Window"), Item(vm, "Minimize"), useOn: PlatformSet.MacOS));
        Assert.NotNull(vm.SelectedCommandId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));

        Assert.Null(vm.SelectedCommandId);
        Assert.Equal(0, vm.PlatformFilterNudges);
    }

    [AvaloniaFact]
    public void TheTreeBlinksItsToggleAtEachNudge_WhichHasATooltip()
    {
        var tree = new CommandTree { PlatformFilterLabel = "Show other platforms" };
        var window = new Window { Content = tree, Width = 400, Height = 400 };
        window.Show();
        var toggle = tree.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name == "PART_OtherPlatforms");
        Assert.Equal(CommandTree.PlatformFilterHelp, ToolTip.GetTip(toggle));
        Assert.DoesNotContain("nudge", toggle.Classes);

        tree.PlatformFilterNudges = 1;

        Assert.Contains("nudge", toggle.Classes);
    }
}
