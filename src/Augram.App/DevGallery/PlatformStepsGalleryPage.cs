#if DEBUG
using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Augram.App.DevGallery;

/// <summary>
/// A command's steps per platform (plan 0010), each panel live over its own throwaway store: Magnifier (Shift+Win+X on
/// Windows, its own Opt+Shift+X on macOS) seen from Windows on both sides of the "Steps for" switch, the macOS side read-only
/// with "Edit on macOS" and Copy from on each; Close tab (Ctrl+W on Windows only) seen from a Mac, its macOS side inferred and
/// its Windows side read-only; and the app identification form with the other platform's fields read-only (decision 7).
/// </summary>
public static class PlatformStepsGalleryPage
{
    public static ScreenDeclaration Page()
    {
        var identification = new AppMatcherEditViewModel(() => PlatformSet.All, "type its name", "none needed") { Platform = HostPlatform.Windows };
        identification.SyncFrom(new AppMatcher { WindowsProcessNames = ["chrome.exe"], MacProcessNames = ["Google Chrome"], RootClass = "Chrome_WidgetWin_1" });
        identification.View.Shown = HostPlatform.MacOS;
        return new FormScreen("Steps per platform",
        [
            new Section("Magnifier seen from Windows: the Windows side runs here, Copy from macOS",
            [
                new CustomField("Windows side", () => Panel(HostPlatform.Windows, "Magnifier", HostPlatform.Windows)),
            ]),
            new Section("Magnifier's macOS side seen from Windows: its own steps, read-only",
            [
                new CustomField("macOS side", () => Panel(HostPlatform.Windows, "Magnifier", HostPlatform.MacOS)),
            ]),
            new Section("Close tab seen from a Mac: the macOS side converted from Windows, no Copy",
            [
                new CustomField("macOS side", () => Panel(HostPlatform.MacOS, "Close tab", HostPlatform.MacOS)),
            ]),
            new Section("Close tab's Windows side seen from a Mac: the original, read-only",
            [
                new CustomField("Windows side", () => Panel(HostPlatform.MacOS, "Close tab", HostPlatform.Windows)),
            ]),
            .. identification.Sections(),
        ]);
    }

    /// <summary>The header and the steps of <paramref name="name"/> on <paramref name="here"/>, with <paramref name="side"/>'s steps in view; the Steps for row is the steps card's first row, as on the workbench.</summary>
    private static StackPanel Panel(HostPlatform here, string name, HostPlatform side)
    {
        var library = new GestureLibrary(StarterGestures.All());
        var store = new MappingStore(Mapping());
        var vm = new CommandsViewModel(CommandsScope.Global, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), here);
        vm.ShowCommand(store.Current.Global.Commands.Single(command => command.Name == name).Id);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ShowStepsFor, command: vm.SelectedCommand, platform: side));

        var header = new CommandHeader();
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        var stepSide = new StepSideBar();
        stepSide.Bind(StepSideBar.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        stepSide.ActionRequested += (_, e) => vm.Handle(e);
        var steps = new StepList { StepTypes = vm.StepTypes, HasCommand = true, CardHeader = stepSide };
        steps.Bind(StepList.StepsProperty, new Binding(nameof(CommandsViewModel.Steps)) { Source = vm });
        steps.Bind(StepList.SelectedIndexProperty, new Binding(nameof(CommandsViewModel.SelectedStepIndex)) { Source = vm });
        steps.Bind(StepList.OpenIndexesProperty, new Binding(nameof(CommandsViewModel.OpenStepIndexes)) { Source = vm });
        steps.Bind(StepList.IsReadOnlyProperty, new Binding($"{nameof(CommandsViewModel.SelectedCommand)}.{nameof(CommandHeaderItem.StepSide)}.{nameof(StepSide.IsHere)}") { Source = vm, Converter = BoolConverters.Not });
        steps.ActionRequested += (_, e) => vm.Handle(e);
        return new StackPanel { Width = 760, Children = { header, steps } };
    }

    private static MappingDocument Mapping()
    {
        var magnifier = new Command(CommandId.New(), "Magnifier", Trigger.None, IsActive: true, [Hotkey(KeyModifiers.Shift | KeyModifiers.Meta, KeyCode.X, HostPlatform.Windows)])
            .WithStepsFor(HostPlatform.MacOS, [Hotkey(KeyModifiers.Alt | KeyModifiers.Shift, KeyCode.X, HostPlatform.MacOS)], DateTimeOffset.UnixEpoch);
        var closeTab = new Command(CommandId.New(), "Close tab", Trigger.None, IsActive: true, [Hotkey(KeyModifiers.Control, KeyCode.W, HostPlatform.Windows)]);
        return MappingRules.ValidDocument(new MappingDocument([AppGroup.EmptyGlobal with { Commands = [magnifier, closeTab] }], []));
    }

    private static CommandStep Hotkey(KeyModifiers modifiers, KeyCode key, HostPlatform authoredOn) => new(new HotkeyStep(modifiers, key), authoredOn);
}
#endif
