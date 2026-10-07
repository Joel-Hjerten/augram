using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Delay;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>F8 own versions on the Commands tab, seen from a Mac (Joel, 2026-10-07): the first edit here makes the command's own steps.</summary>
public sealed class CommandsViewModelVersionTests
{
    [AvaloniaFact]
    public void AWindowsCommandShowsItIsConvertedHere_AndTheFirstEditMakesOwnStepsFromTheConvertedOriginal()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var three = Item(vm, "Three steps");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), three));

        Assert.StartsWith("Windows original, converted for macOS.", vm.SelectedCommand!.VersionText, StringComparison.Ordinal);
        Assert.False(vm.SelectedCommand.HasOwnVersionHere);
        Assert.Equal(HostPlatform.MacOS, vm.Steps[0].Step.AuthoredOn);

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(99)));

        var stored = Find(store, "Three steps");
        Assert.Equal([10, 20, 30], stored.Steps.Select(step => ((DelayStep)step.Step).Milliseconds));
        Assert.Equal(HostPlatform.MacOS, stored.OwnVersion!.Platform);
        Assert.Equal([10, 99, 30], stored.OwnVersion.Steps.Select(step => ((DelayStep)step.Step).Milliseconds));
        Assert.All(stored.OwnVersion.Steps, step => Assert.Equal(HostPlatform.MacOS, step.AuthoredOn));
        Assert.Contains("now has its own steps here", vm.Message, StringComparison.Ordinal);
        Assert.True(vm.SelectedCommand!.HasOwnVersionHere);
        Assert.Equal("own macOS version", Item(vm, "Three steps").PlatformMarker);
    }

    [AvaloniaFact]
    public void UseTheConvertedOriginalDropsTheOwnSteps_AndMarkAsCheckedClearsTheFlag()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var command = Find(store, "Three steps");
        store.UpdateCommand(store.Global.Id, command.WithStepsFor(HostPlatform.MacOS, [], DateTimeOffset.UnixEpoch));
        store.UpdateCommand(store.Global.Id, Find(store, "Three steps").WithStepsFor(HostPlatform.Windows, [command.Steps[0]], DateTimeOffset.UnixEpoch));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        Assert.True(vm.SelectedCommand!.IsOwnVersionStale);
        Assert.Equal("own macOS version · Windows original changed", Item(vm, "Three steps").PlatformMarker);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.MarkOwnVersionChecked, command: vm.SelectedCommand));
        Assert.False(Find(store, "Three steps").IsOwnVersionStale);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.UseConvertedOriginal, command: vm.SelectedCommand));
        Assert.Null(Find(store, "Three steps").OwnVersion);
        Assert.Contains("runs the converted original here again", vm.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void OnTheOriginsPlatformAnEditChangesTheOriginal()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new DelayStep(11)));

        Assert.Equal(11, ((DelayStep)Find(store, "Three steps").Steps[0].Step).Milliseconds);
        Assert.Null(Find(store, "Three steps").OwnVersion);
        Assert.Null(vm.SelectedCommand!.VersionText);
    }
}
