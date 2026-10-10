#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Avalonia.Data;

namespace Augram.App.DevGallery;

/// <summary>
/// The trigger draft's Take it and Swap (Joel, 2026-10-10), each header live over its own throwaway store
/// (<see cref="CommandGalleryFakes.MappingWithZoom"/>), so its buttons work: Global › Zoom in turned to wheel down, which Zoom
/// out uses (both buttons in the note); Blender › Pan given Orbit's Left (an input: both buttons); and Zoom in with a wheel
/// and no button held (a note without buttons).
/// </summary>
public static class TriggerConflictGalleryPage
{
    public static ScreenDeclaration Page() => new FormScreen("Trigger conflict",
    [
        new Section("Global › Zoom in turned to wheel down, which Zoom out uses: Take it and Swap in the note",
        [
            new CustomField("Zoom in", () => DraftedHeader(CommandsScope.Global, "Zoom in", item => new(CommandTreeAction.SetWheelDirection, command: item, wheel: WheelDirection.Down))),
        ]),
        new Section("Blender › Space › Pan given Left, which Orbit uses: the same for an input",
        [
            new CustomField("Pan", () => DraftedHeader(CommandsScope.Apps, "Pan", item => new(CommandTreeAction.SetInput, command: item, input: HoldInput.Of(MouseButton.Left)))),
        ]),
        new Section("Global › Zoom in holding no button: a note without buttons",
        [
            new CustomField("No button", () => DraftedHeader(CommandsScope.Global, "Zoom in", item => new(CommandTreeAction.SetTriggerHold, command: item, hold: new TriggerHold(HeldButtons.None)))),
        ]),
    ]);

    /// <summary>A command header bound to a view model over a throwaway store, with <paramref name="name"/> selected and the edit <paramref name="draft"/> asks for waiting as its draft.</summary>
    private static CommandHeader DraftedHeader(CommandsScope scope, string name, Func<CommandItem, CommandTreeActionEventArgs> draft)
    {
        var library = new GestureLibrary(StarterGestures.All());
        var store = new MappingStore(CommandGalleryFakes.MappingWithZoom());
        var vm = new CommandsViewModel(scope, store, library, new CommandGalleryFakes.GesturePicker(library), new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), new CommandClipboard(), HostPlatform.Windows);
        var found = store.Current.AllCommands().First(pair => pair.Command.Name == name && CommandSections.Includes(scope, pair.Group));
        vm.ShowCommand(found.Command.Id);
        vm.Handle(draft(vm.SelectedCommand!));
        var header = new CommandHeader { Width = 760 };
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        return header;
    }
}
#endif
