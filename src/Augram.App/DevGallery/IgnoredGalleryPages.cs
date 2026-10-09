#if DEBUG
using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the <see cref="MasterDetail"/> component: the Ignored tab over a throwaway store, and the component's empty and selected states with plain items.</summary>
public static class IgnoredGalleryPages
{
    /// <summary>The Ignored tab over Joel's six imported ignored apps (two inactive), with dialogs that answer at once.</summary>
    public static ScreenDeclaration IgnoredPage()
    {
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], FakeIgnored()));
        var vm = new IgnoredViewModel(store, new CommandGalleryFakes.FormDialogs(), new CommandGalleryFakes.Confirm(), HostPlatform.Windows);
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
            new Section("Three items, the second selected, a message line; the third inactive",
            [
                new CustomField("Selected", () => new MasterDetail
                {
                    Heading = "Ignored apps",
                    NewLabel = "New ignored app",
                    Items = items,
                    SelectedId = items[1].Id,
                    Detail = edit.Declare(),
                    Message = "Deleted 'Libre HW Monitor'. Ctrl+Z undoes it.",
                    Height = 520,
                }),
            ]),
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
