using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.WindowOp;
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

        Assert.Equal("macOS · runs here · converted from Windows", vm.SelectedCommand!.StepSide!.Label);
        Assert.Equal(HostPlatform.MacOS, vm.Steps[0].Step.AuthoredOn);

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(99)));

        var stored = Find(store, "Three steps");
        Assert.Equal([10, 20, 30], stored.Steps.Select(step => ((DelayStep)step.Step).Milliseconds));
        Assert.Equal(HostPlatform.MacOS, stored.OwnVersion!.Platform);
        Assert.Equal([10, 99, 30], stored.OwnVersion.Steps.Select(step => ((DelayStep)step.Step).Milliseconds));
        Assert.All(stored.OwnVersion.Steps, step => Assert.Equal(HostPlatform.MacOS, step.AuthoredOn));
        Assert.Contains("now has its own steps here", vm.Message, StringComparison.Ordinal);
        Assert.Equal("macOS · runs here", vm.SelectedCommand!.StepSide!.Label);
        Assert.Equal("own macOS version", Item(vm, "Three steps").PlatformMarker);
    }

    /// <summary>A step with no conversion stays Windows' in the own version; once edited here it is a macOS step (Joel, 0.13.9: the magnifier).</summary>
    [AvaloniaFact]
    public void AnUnconvertedStepEditedHere_IsAuthoredHere()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var command = Find(store, "Three steps");
        store.UpdateCommand(store.Global.Id, command with { Steps = [new CommandStep(new HotkeyStep(KeyModifiers.Shift | KeyModifiers.Meta, KeyCode.X), HostPlatform.Windows)] });
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));
        Assert.Equal(HostPlatform.Windows, vm.Steps[0].Step.AuthoredOn);

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new HotkeyStep(KeyModifiers.Alt | KeyModifiers.Shift, KeyCode.X)));

        var own = Assert.Single(Find(store, "Three steps").OwnVersion!.Steps);
        Assert.Equal(HostPlatform.MacOS, own.AuthoredOn);
        Assert.Equal("Opt+Shift+X", vm.Steps[0].Summary);
    }

    /// <summary>"Change to…" edits this platform's steps as Edit does: the first change here makes own steps, the original keeps its step.</summary>
    [AvaloniaFact]
    public void ChangingAStepsTypeHere_MakesOwnStepsFromTheConvertedOriginal_TheOriginalKeepsItsStep()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        vm.Handle(new StepListActionEventArgs(StepListAction.ChangeType, vm.Steps[1], type: WindowOpStepType.Instance));

        var stored = Find(store, "Three steps");
        Assert.Equal([10, 20, 30], stored.Steps.Select(step => ((DelayStep)step.Step).Milliseconds));
        Assert.Equal(HostPlatform.MacOS, stored.OwnVersion!.Platform);
        Assert.Equal(["Wait 10 ms", "Minimize window", "Wait 30 ms"], stored.OwnVersion.Steps.Select(step => step.Step.Summary));
        Assert.All(stored.OwnVersion.Steps, step => Assert.Equal(HostPlatform.MacOS, step.AuthoredOn));
        Assert.Contains("now has its own steps here", vm.Message, StringComparison.Ordinal);
        Assert.Equal(1, vm.SelectedStepIndex);
    }

    [AvaloniaFact]
    public void MarkAsCheckedClearsTheFlag()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var command = Find(store, "Three steps");
        store.UpdateCommand(store.Global.Id, command.WithStepsFor(HostPlatform.MacOS, [], DateTimeOffset.UnixEpoch));
        store.UpdateCommand(store.Global.Id, Find(store, "Three steps").WithStepsFor(HostPlatform.Windows, [command.Steps[0]], DateTimeOffset.UnixEpoch));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        Assert.True(vm.SelectedCommand!.IsOwnVersionStale);
        Assert.Equal("own macOS version · Windows original changed", Item(vm, "Three steps").PlatformMarker);
        Assert.Equal("macOS · runs here · Windows original changed", vm.SelectedCommand.StepSide!.Label);
        Assert.True(vm.SelectedCommand.StepSide.CanMarkChecked);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.MarkOwnVersionChecked, command: vm.SelectedCommand));
        Assert.False(Find(store, "Three steps").IsOwnVersionStale);
        Assert.False(vm.SelectedCommand!.StepSide!.CanMarkChecked);
    }

    /// <summary>Plan 0010 decisions 1 and 5: the switch opens on this platform, and Copy is offered on neither side of an inferred one.</summary>
    [AvaloniaFact]
    public void TheStepsSwitchOpensOnThisPlatform_AndAnotherSelectionBringsItBack()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));
        Assert.Equal(HostPlatform.MacOS, vm.SelectedCommand!.StepSide!.Shown);
        Assert.Null(vm.SelectedCommand.StepSide.CopyText);

        ShowStepsFor(vm, HostPlatform.Windows);
        var side = vm.SelectedCommand!.StepSide!;
        Assert.Equal(HostPlatform.Windows, side.Shown);
        Assert.False(side.IsHere);
        Assert.Equal("Windows · own steps", side.Label);
        Assert.Equal("Edit on Windows", side.EditNote);
        Assert.Null(side.CopyText);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window"), Item(vm, "Minimize")));
        Assert.Equal(HostPlatform.MacOS, vm.SelectedCommand!.StepSide!.Shown);
    }

    /// <summary>Plan 0010 decision 6: the other platform's steps read in its key names, and nothing edits them from here.</summary>
    [AvaloniaFact]
    public void TheOtherSideReadsInItsKeyNames_AndNoEditReachesTheStore()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var cmdBackspace = new CommandStep(new HotkeyStep(KeyModifiers.Meta, KeyCode.Backspace), HostPlatform.MacOS);
        store.UpdateCommand(store.Global.Id, Find(store, "Three steps") with { Steps = [cmdBackspace] });
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));
        ShowStepsFor(vm, HostPlatform.Windows);
        var before = Find(store, "Three steps");

        Assert.EndsWith("→ Ctrl+Backspace", vm.Steps[0].Summary, StringComparison.Ordinal);
        Assert.Equal("Windows · converted from macOS", vm.SelectedCommand!.StepSide!.Label);
        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new DelayStep(99)));
        vm.Handle(new StepListActionEventArgs(StepListAction.Delete, vm.Steps[0]));
        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: DelayStepType.Instance));

        Assert.Same(before, Find(store, "Three steps"));
        Assert.Contains("Edit on Windows", vm.Message, StringComparison.Ordinal);
    }

    /// <summary>Plan 0010 decision 5: Copy from replaces the side in view with the other side's steps; one undo gives them back.</summary>
    [AvaloniaFact]
    public void CopyOnEitherSideTakesTheOtherSidesSteps_AndOneUndoBringsThemBack()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        var macStep = new CommandStep(new DelayStep(50), HostPlatform.MacOS);
        store.UpdateCommand(store.Global.Id, Find(store, "Three steps").WithStepsFor(HostPlatform.MacOS, [macStep], DateTimeOffset.UnixEpoch));
        var withMac = Find(store, "Three steps");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        Assert.Equal("Copy from Windows", vm.SelectedCommand!.StepSide!.CopyText);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.CopyStepsFromOtherPlatform, command: vm.SelectedCommand));
        Assert.Null(Find(store, "Three steps").OwnVersion);
        Assert.Equal([10, 20, 30], vm.Steps.Select(step => ((DelayStep)step.Step.Step).Milliseconds));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(withMac, Find(store, "Three steps"));

        ShowStepsFor(vm, HostPlatform.Windows);
        Assert.Equal("Copy from macOS", vm.SelectedCommand!.StepSide!.CopyText);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.CopyStepsFromOtherPlatform, command: vm.SelectedCommand));
        var copied = Find(store, "Three steps");
        Assert.Null(copied.OwnVersion);
        Assert.Equal([macStep], copied.StepsFor(HostPlatform.Windows));
        Assert.Equal("Windows · converted from macOS", vm.SelectedCommand!.StepSide!.Label);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(withMac, Find(store, "Three steps"));
    }

    [AvaloniaFact]
    public void OnTheOriginsPlatformAnEditChangesTheOriginal()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new DelayStep(11)));

        Assert.Equal(11, ((DelayStep)Find(store, "Three steps").Steps[0].Step).Milliseconds);
        Assert.Null(Find(store, "Three steps").OwnVersion);
        Assert.Equal("Windows · runs here", vm.SelectedCommand!.StepSide!.Label);
    }

    private static void ShowStepsFor(CommandsViewModel vm, HostPlatform platform)
        => vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ShowStepsFor, command: vm.SelectedCommand, platform: platform));
}
