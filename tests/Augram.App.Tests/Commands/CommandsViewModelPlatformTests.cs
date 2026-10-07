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
    public void BothTabsHaveThePlatformFilter()
    {
        Assert.Equal("Show other platforms", Create().Vm.PlatformFilterLabel);
        Assert.Equal("Show other platforms", Create(CommandsScope.Global).Vm.PlatformFilterLabel);
    }

    [AvaloniaFact]
    public void ACommandNotUsedHereIsHidden_TheFilterShowsItGreyedWithItsPlatform()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var minimize = Item(vm, "Minimize");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, Section(vm, "Window"), minimize, useOn: PlatformSet.Windows));

        Assert.Equal(PlatformSet.Windows, Find(store, "Minimize").UseOn);
        Assert.DoesNotContain(vm.Sections.SelectMany(section => section.Commands), command => command.Name == "Minimize");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));

        var shown = Item(vm, "Minimize");
        Assert.True(shown.IsElsewhere);
        Assert.Equal("Windows only", shown.PlatformMarker);
    }

    [AvaloniaFact]
    public void ACommandUsedNowhereIsRefusedWithTheRule()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, Section(vm, "Window"), Item(vm, "Minimize"), useOn: PlatformSet.None));

        Assert.Equal("Use 'Minimize' on at least one platform.", vm.Message);
        Assert.Equal(PlatformSet.All, Find(store, "Minimize").UseOn);
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

        UseOn("macOS", vm.GroupForm!).Set(false);

        Assert.Equal(PlatformSet.Windows, Group(store, "Chrome").UseOn);
        Assert.DoesNotContain("Chrome", Names(vm));
        Assert.Null(vm.GroupForm);
    }

    private static IValueBinding<bool> UseOn(string platform, FormScreen screen)
        => screen.Sections.SelectMany(section => section.Fields).OfType<TogglesField>().Single(field => field.Label == "Use on").Options.Single(option => option.Caption == platform).Value;
}
