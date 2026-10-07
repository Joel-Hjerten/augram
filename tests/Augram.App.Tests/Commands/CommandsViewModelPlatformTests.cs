using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>F8 per platform on the Apps tab, seen from a Mac (Joel, 2026-10-07): "Use on", the filter, the notes and the guess.</summary>
public sealed class CommandsViewModelPlatformTests
{
    [AvaloniaFact]
    public void AGroupNotUsedHereIsHidden_TheFilterShowsItGreyedWithItsPlatform()
    {
        var (vm, store, _, _) = Create(platform: HostPlatform.MacOS);
        store.UpdateGroup(Group(store, "Photoshop") with { UseOn = PlatformSet.Windows });

        Assert.Equal(["Apple", "Chrome"], Names(vm));
        Assert.False(vm.ShowOtherPlatforms);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));

        Assert.True(vm.ShowOtherPlatforms);
        var photoshop = Section(vm, "Photoshop");
        Assert.True(photoshop.IsElsewhere);
        Assert.Equal("2 commands · Windows only", photoshop.CountText);
        Assert.False(Section(vm, "Chrome").IsElsewhere);
    }

    [AvaloniaFact]
    public void AGroupUsedHereWithoutANameOrGuessForThisPlatformSaysSo()
    {
        var (vm, _, _, _) = Create(platform: HostPlatform.MacOS);

        Assert.Equal("no commands · no macOS name", Section(vm, "Apple").CountText);
        Assert.Equal("2 commands", Section(vm, "Chrome").CountText);
    }

    [AvaloniaFact]
    public void OnlyTheAppsTabHasThePlatformFilter()
    {
        Assert.Equal("Show other platforms", Create().Vm.PlatformFilterLabel);
        Assert.Null(Create(CommandsScope.Global).Vm.PlatformFilterLabel);
    }

    [AvaloniaFact]
    public void TheFormShowsTheGuessForAnEmptyList()
    {
        var (_, store, _, _) = Create();

        var chrome = GroupEditViewModel.From(Group(store, "Chrome"));
        Assert.Equal("macOS: Google Chrome (guessed)", chrome.GuessText);
        chrome.UseOnMac = false;
        Assert.StartsWith("None needed", chrome.GuessText, StringComparison.Ordinal);

        var apple = GroupEditViewModel.From(Group(store, "Apple"));
        Assert.Equal("macOS: no guess; type its name, or stop using the group there", apple.GuessText);
        apple.MacNames = "Apple";
        Assert.StartsWith("None needed", apple.GuessText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void UntickingThisPlatformInThePanelStoresItAndTheGroupLeavesTheList()
    {
        var (vm, store, _, _) = Create(platform: HostPlatform.MacOS);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Chrome")));

        Toggle("Use on macOS", vm.GroupForm!).Set(false);

        Assert.Equal(PlatformSet.Windows, Group(store, "Chrome").UseOn);
        Assert.DoesNotContain("Chrome", Names(vm));
        Assert.Null(vm.GroupForm);
    }

    private static IValueBinding<bool> Toggle(string label, FormScreen screen)
        => (IValueBinding<bool>)screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;
}
