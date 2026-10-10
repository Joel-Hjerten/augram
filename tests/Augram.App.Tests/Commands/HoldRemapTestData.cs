using Augram.App.Components.CommandTree;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;

namespace Augram.App.Tests.Commands;

/// <summary>
/// Joel's Blender for the hold remap editor (plan 0002 step 4): Global with Close window on Up; Chrome with Close tab on Down;
/// Blender with an ordinary Undo (Left) and the Space hold remap holding Orbit (Left → Middle), Pan (Right → Shift + Middle),
/// Zoom both (Left + Right → Ctrl + Middle) and Grab (W → G). Fresh ids per call.
/// </summary>
internal static class HoldRemapTestData
{
    public static MappingStore Store()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var global = AppGroup.EmptyGlobal with { Commands = [Plain("Close window", Trigger.ForGesture(CommandsTestData.Up))] };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] }, [Plain("Close tab", Trigger.ForGesture(CommandsTestData.Down))]);
        var blender = new AppGroup(GroupId.New(), "Blender", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["blender.exe"] },
            [
                Plain("Undo", Trigger.ForGesture(CommandsTestData.Left)),
                Remap(space, "Orbit", HoldInput.Of(MouseButton.Left), new RemapOutput.Button(MouseButton.Middle)),
                Remap(space, "Pan", HoldInput.Of(MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)),
                Remap(space, "Zoom both", HoldInput.Of(MouseButton.Left, MouseButton.Right), new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Control)),
                Remap(space, "Grab", new HoldInput.Key(KeyCode.W), new RemapOutput.Key(KeyCode.G)),
            ])
        {
            HoldRemaps = [space],
        };
        return new MappingStore(new MappingDocument([global, chrome, blender], []));
    }

    /// <summary>One tab's view model over a fresh <see cref="Store"/>, with the confirm presenter and the clipboard it uses.</summary>
    public static (CommandsViewModel Vm, MappingStore Store, FakeConfirmPresenter Confirm) CreateBlender(CommandsScope scope = CommandsScope.Apps, CommandClipboard? clipboard = null)
    {
        var store = Store();
        var confirm = new FakeConfirmPresenter();
        var vm = new CommandsViewModel(scope, store, new GestureLibrary(StarterGestures.All()), new FakeGesturePickerPresenter(), new FakeFormDialogPresenter(), confirm, clipboard ?? new CommandClipboard(), HostPlatform.Windows);
        return (vm, store, confirm);
    }

    public static AppGroup Blender(MappingStore store) => CommandsTestData.Group(store, "Blender");

    public static HoldRemap Space(MappingStore store) => Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Name == "Space");

    /// <summary>The hold remap's section in the tree.</summary>
    public static SectionItem HoldRemapSection(CommandsViewModel vm, string name) => vm.Sections.Single(section => section.IsNested && section.Name == name);

    public static Command Remap(HoldRemap holdRemap, string name, HoldInput input, RemapOutput output)
        => new Command(CommandId.New(), name, Trigger.ForInput(input), IsActive: true, [new CommandStep(new RemapStep(output), HostPlatform.Windows)]) { HoldRemapId = holdRemap.Id };

    private static Command Plain(string name, Trigger trigger) => new(CommandId.New(), name, trigger, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);
}
