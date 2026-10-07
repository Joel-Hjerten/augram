using Augram.App.Components.CommandTree;
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
/// A small mapping over the starter gestures. Global has the categories Window (Close window on Up,
/// Minimize on Down) and Media (Volume up on wheel up), given out of name order, plus an uncategorized
/// three-step command. "Chrome" has an override to nothing on Up and Close tab on Down with a macOS
/// override on its step; "Apple" is empty; "Photoshop" has the categories General and Blend Mode Normal,
/// Brush on Left in General and an uncategorized Plain. So sorting, sections, tags, summaries and
/// markers all have a case.
/// </summary>
internal static class CommandsTestData
{
    public static GestureId Up => StarterGestures.IdFor("Up");

    public static GestureId Down => StarterGestures.IdFor("Down");

    public static GestureId Left => StarterGestures.IdFor("Left");

    public static MappingStore Store()
    {
        var window = new CommandCategory(CategoryId.New(), "Window");
        var media = new CommandCategory(CategoryId.New(), "Media");
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [window, media],
            Commands =
            [
                Command("Close window", Trigger.ForGesture(Up), new WindowOpStep(WindowOperation.Close)) with { CategoryId = window.Id },
                Command("Minimize", Trigger.ForGesture(Down), new WindowOpStep(WindowOperation.Minimize)) with { CategoryId = window.Id },
                Command("Volume up", Trigger.ForWheel(WheelDirection.Up), new MediaKeyStep(MediaKeyKind.VolumeUp)) with { CategoryId = media.Id },
                Command("Three steps", Trigger.None, new DelayStep(10), new DelayStep(20), new DelayStep(30)),
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] },
        [
            Command("Nothing on Up", Trigger.ForGesture(Up)),
            new Command(CommandId.New(), "Close tab", Trigger.ForGesture(Down), IsActive: true,
                [new CommandStep(new DelayStep(5), HostPlatform.Windows, MacOsOverride: new DelayStep(50))]),
        ]);
        var apple = new AppGroup(GroupId.New(), "Apple", IsActive: true, SuppressGlobals: true, new AppMatcher { WindowsProcessNames = ["apple.exe"] }, []);
        var general = new CommandCategory(CategoryId.New(), "General");
        var blend = new CommandCategory(CategoryId.New(), "Blend Mode Normal");
        var photoshop = new AppGroup(GroupId.New(), "Photoshop", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["photoshop.exe"] },
            [
                Command("Brush", Trigger.ForGesture(Left), new DelayStep(1)) with { CategoryId = general.Id },
                Command("Plain", Trigger.None, new DelayStep(2)),
            ],
            [general, blend]);
        return new MappingStore(new MappingDocument([global, chrome, apple, photoshop], []));
    }

    /// <summary>One tab's view model over a fresh <see cref="Store"/>; the Apps tab unless told otherwise.</summary>
    public static (CommandsViewModel Vm, MappingStore Store, FakeGesturePickerPresenter Picker, FakeFormDialogPresenter Dialogs) Create(
        CommandsScope scope = CommandsScope.Apps,
        FakeConfirmPresenter? confirm = null,
        HostPlatform platform = HostPlatform.Windows)
    {
        var store = Store();
        var picker = new FakeGesturePickerPresenter();
        var dialogs = new FakeFormDialogPresenter();
        var vm = New(scope, store, picker, dialogs, confirm ?? new FakeConfirmPresenter(), new CommandClipboard(), platform);
        return (vm, store, picker, dialogs);
    }

    /// <summary>Both tabs over one store and one clipboard, as the app composes them.</summary>
    public static (CommandsViewModel Global, CommandsViewModel Apps, MappingStore Store) CreateBoth()
    {
        var store = Store();
        var clipboard = new CommandClipboard();
        var confirm = new FakeConfirmPresenter();
        return (
            New(CommandsScope.Global, store, new FakeGesturePickerPresenter(), new FakeFormDialogPresenter(), confirm, clipboard),
            New(CommandsScope.Apps, store, new FakeGesturePickerPresenter(), new FakeFormDialogPresenter(), confirm, clipboard),
            store);
    }

    public static Command Find(MappingStore store, string name) => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;

    public static AppGroup Group(MappingStore store, string name) => store.Current.Groups.Single(group => group.Name == name);

    public static CommandCategory Category(MappingStore store, string name) => store.Global.Categories.Single(category => category.Name == name);

    public static SectionItem Section(CommandsViewModel vm, string name) => vm.Sections.Single(section => section.Name == name);

    public static CommandItem Item(CommandsViewModel vm, string name) => vm.Sections.SelectMany(section => section.Commands).Single(command => command.Name == name);

    public static IReadOnlyList<string> Names(CommandsViewModel vm) => [.. vm.Sections.Select(section => section.Name)];

    private static CommandsViewModel New(CommandsScope scope, MappingStore store, FakeGesturePickerPresenter picker, FakeFormDialogPresenter dialogs, FakeConfirmPresenter confirm, CommandClipboard clipboard, HostPlatform platform = HostPlatform.Windows)
        => new(scope, store, new GestureLibrary(StarterGestures.All()), picker, dialogs, confirm, clipboard, platform);

    private static Command Command(string name, Trigger trigger, params IStep[] steps)
        => new(CommandId.New(), name, trigger, IsActive: true, [.. steps.Select(step => new CommandStep(step, HostPlatform.Windows))]);
}
