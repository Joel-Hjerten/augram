using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;

namespace Augram.App.Tests.Commands;

/// <summary>
/// A small mapping over the starter gestures: Global (Close window on Up, Volume up on wheel up, an
/// unbound three-step command), "Chrome" (an override to nothing on Up, Close tab on Down with a
/// macOS override on its step) and "Apple" (empty), so sorting, summaries and markers all have a case.
/// </summary>
internal static class CommandsTestData
{
    public static GestureId Up => StarterGestures.IdFor("Up");

    public static GestureId Down => StarterGestures.IdFor("Down");

    public static MappingStore Store()
    {
        var global = AppGroup.EmptyGlobal with
        {
            Commands =
            [
                Command("Close window", Trigger.ForGesture(Up), new WindowOpStep(WindowOperation.Close)),
                Command("Volume up", Trigger.ForWheel(WheelDirection.Up), new MediaKeyStep(MediaKeyKind.VolumeUp)),
                Command("Three steps", Trigger.None, new DelayStep(10), new DelayStep(20), new DelayStep(30)),
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { ProcessNames = ["chrome.exe"] },
        [
            Command("Nothing on Up", Trigger.ForGesture(Up)),
            new Command(CommandId.New(), "Close tab", Trigger.ForGesture(Down), IsActive: true,
                [new CommandStep(new DelayStep(5), HostPlatform.Windows, MacOsOverride: new DelayStep(50))]),
        ]);
        var apple = new AppGroup(GroupId.New(), "Apple", IsActive: true, SuppressGlobals: true, new AppMatcher { ProcessNames = ["apple.exe"] }, []);
        return new MappingStore(new MappingDocument([global, chrome, apple], []));
    }

    public static (CommandsViewModel Vm, MappingStore Store, FakeGesturePickerPresenter Picker, FakeFormDialogPresenter Dialogs) Create()
        => Create(new FakeConfirmPresenter());

    public static (CommandsViewModel Vm, MappingStore Store, FakeGesturePickerPresenter Picker, FakeFormDialogPresenter Dialogs) Create(FakeConfirmPresenter confirm)
    {
        var store = Store();
        var picker = new FakeGesturePickerPresenter();
        var dialogs = new FakeFormDialogPresenter();
        var vm = new CommandsViewModel(store, new GestureLibrary(StarterGestures.All()), picker, dialogs, confirm, HostPlatform.Windows);
        return (vm, store, picker, dialogs);
    }

    public static Command Find(MappingStore store, string name) => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;

    public static AppGroup Group(MappingStore store, string name) => store.Current.Groups.Single(group => group.Name == name);

    private static Command Command(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, [.. steps.Select(step => new CommandStep(step, HostPlatform.Windows))]);
}
