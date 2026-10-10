#if DEBUG
using Augram.App.Components.MasterDetail;
using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the <see cref="MasterDetail"/> component (the Ignored tab over a throwaway store, and the component's empty and selected states with plain items) and the <see cref="WindowFinder"/> on the identification form.</summary>
public static class IgnoredGalleryPages
{
    /// <summary>Ignored › Global over Joel's six imported ignored apps (two inactive), with dialogs that answer at once.</summary>
    public static ScreenDeclaration IgnoredPage()
    {
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], FakeIgnored()));
        var vm = new IgnoredViewModel(store, new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), HostPlatform.Windows);
        return Screens.IgnoredScreen.Declare(vm);
    }

    /// <summary>
    /// Ignored › Per command (plan 0004) over <see cref="CommandGalleryFakes.MappingWithZoom"/>: Eyeris (used by Global › Media ›
    /// Zoom in and Chrome › Zoom in), Krita (inactive, not used) and Spine, selected, its form with "Used by" Global › Media ›
    /// Zoom in as a link (the gallery's locator opens nothing). Right-click offers Move to Global.
    /// </summary>
    public static ScreenDeclaration PerCommandPage()
    {
        var store = new MappingStore(CommandGalleryFakes.MappingWithZoom());
        var vm = new IgnoredViewModel(store, new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), HostPlatform.Windows, IgnoreScope.PerCommand, new CommandGalleryFakes.Locator());
        var spine = vm.Items.Single(item => item.Name == "Spine");
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, spine));
        return Screens.IgnoredScreen.Declare(vm);
    }

    /// <summary>The bare component: nothing in the list, then three items with the second selected and its form beside it.</summary>
    public static ScreenDeclaration MasterDetailPage()
    {
        var edit = new IgnoredEditViewModel { Name = "Blender" };
        edit.Identification.WindowsNames = "blender.exe";
        MasterItem[] items =
        [
            new(Guid.NewGuid(), "A Plague Tale", true, "Gestures off over this app · APlagueTaleRequiem_x64.exe"),
            new(Guid.NewGuid(), "Blender", true, "Gestures off over this app · blender.exe"),
            new(Guid.NewGuid(), "Spine", false, "Gestures off over this app · Spine.exe"),
        ];
        return new FormScreen("MasterDetail",
        [
            new Section("Empty: the help line beside an empty list",
            [
                new CustomField("Empty", () => new MasterDetail { Heading = "Things", NewLabel = "New thing", HelpText = "A help line under the list.", EmptyDetailText = "Select a thing.", Height = 220 }),
            ]),
            new Section("Three items, the second selected, a message line; the third inactive; the menu has a move entry",
            [
                new CustomField("Selected", () => new MasterDetail
                {
                    Heading = "Ignored apps",
                    NewLabel = "New ignored app",
                    MoveLabel = "Move to Per command",
                    Items = items,
                    SelectedId = items[1].Id,
                    Detail = edit.Declare(),
                    Message = "Deleted 'Libre HW Monitor'. Ctrl+Z undoes it.",
                    Height = 520,
                }),
            ]),
        ]);
    }

    /// <summary>
    /// The window finder: a bare magnifier whose last pick shows beside it, and the identification form over a pretend Chrome
    /// group (a magnifier on each of this machine's rows, Use Regex beside each). Drags read the real windows on screen
    /// through the window system the app publishes; nothing is written anywhere.
    /// </summary>
    public static ScreenDeclaration WindowFinderPage()
    {
        var last = "Nothing picked yet.";
        var lastBinding = new DelegateBinding<string>(() => last, propertyName: null);
        var edit = new AppMatcherEditViewModel(() => PlatformSet.All, "type its name", "none needed");
        edit.SyncFrom(new AppMatcher { WindowsProcessNames = ["chrome.exe"], MacProcessNames = ["Google Chrome"], RootClass = "Chrome_WidgetWin_1", ControlClassIsRegex = true, ControlClass = "^Chrome_" });
        return new FormScreen("WindowFinder",
        [
            new Section("Bare: press, drag onto any window, release; Esc or a right-click cancels",
            [
                new NoteField("Last pick", lastBinding, "Over Augram's own windows or empty screen nothing is picked.")
                {
                    Accessory = WindowFinderAccessory.Finder(WindowFinder.Summary, window =>
                    {
                        last = $"{WindowFinder.Summary(window)} · {window.ProcessPath ?? "no path"} · {string.Join(", ", window.ClassChain)}";
                        lastBinding.NotifyChanged();
                    }),
                },
            ]),
            .. edit.Sections(),
        ]);
    }

    private static IgnoredApp[] FakeIgnored() =>
    [
        Ignored("VMware", "vmware.exe", disableWhileFocused: true),
        Ignored("Blender", "blender.exe"),
        Ignored("A Plague Tale", "APlagueTaleRequiem_x64.exe"),
        Ignored("DaVinci Resolve", "Resolve.exe"),
        Ignored("Spine", "Spine.exe") with { IsActive = false },
        Ignored("Libre HW Monitor", "LibreHardwareMonitor.exe") with { IsActive = false },
    ];

    private static IgnoredApp Ignored(string name, string executable, bool disableWhileFocused = false)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [executable] }, disableWhileFocused);
}
#endif
