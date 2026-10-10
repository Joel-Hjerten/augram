#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Components.SyncConflictList;
using Augram.App.Declarations;
using Augram.App.Sync;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Sync;

namespace Augram.App.DevGallery;

/// <summary>Gallery pages for the two sync dialogs (F8 sync): the join question and the conflict list, over fake machines and fake conflicts of every kind.</summary>
public static class SyncGalleryPages
{
    private static readonly IReadOnlyList<Gesture> Starter = StarterGestures.All();

    public static ScreenDeclaration JoinPage() =>
        new FormScreen("Sync join",
        [
            new Section("Two machines already in the repository (the newest is adopted)",
            [
                new CustomField("Join sync", () => JoinDialog(
                [
                    new SyncMachineSummary(Guid.NewGuid(), "PC-HOME", new DateTimeOffset(2026, 10, 7, 7, 40, 0, TimeSpan.Zero), 105, 9, 212),
                    new SyncMachineSummary(Guid.NewGuid(), "Mac", new DateTimeOffset(2026, 10, 6, 17, 5, 0, TimeSpan.Zero), 98, 8, 190),
                ])),
            ]),
            new Section("One machine",
            [
                new CustomField("Join sync", () => JoinDialog([new SyncMachineSummary(Guid.NewGuid(), "PC-WORK", new DateTimeOffset(2026, 10, 7, 9, 12, 0, TimeSpan.Zero), 3, 2, 4)])),
            ]),
        ]);

    public static ScreenDeclaration ConflictsPage()
    {
        var viewModel = new SyncConflictsViewModel(Conflicts(), Starter, Mapping());
        return new FormScreen("Sync conflicts",
        [
            new Section(viewModel.Summary,
            [
                new CustomField("SyncConflictList", () =>
                {
                    var list = new SyncConflictList { Entries = viewModel.Entries, Width = 900, Height = 360 };
                    list.ChoiceChanged += (_, e) => viewModel.Choose(e.Index, e.Choice);
                    return list;
                }),
            ], "A gesture changed on both machines (glyphs side by side), a command, an app group, a category deleted there, a hold remap's tap time, an ignored app deleted here. Keep both only on the gesture and the command."),
            new Section("No conflicts",
            [
                new CustomField("Empty", () => new SyncConflictList { Width = 900, Height = 80 }),
            ]),
        ]);
    }

    private static FormDialog JoinDialog(IReadOnlyList<SyncMachineSummary> machines)
        => new() { Message = SyncJoinViewModel.Message, Screen = new SyncJoinViewModel(machines).Declare(), ConfirmLabel = SyncJoinViewModel.ConfirmLabel, Width = SyncJoinPresenter.WindowWidth };

    private static MappingDocument Mapping()
    {
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, AppMatcher.Empty with { WindowsProcessNames = ["chrome.exe"] }, []);
        var blender = new AppGroup(GroupId.New(), "Blender", IsActive: true, SuppressGlobals: false, AppMatcher.Empty with { WindowsProcessNames = ["blender.exe"] }, []);
        return new MappingDocument([AppGroup.EmptyGlobal, chrome, blender], []);
    }

    private static IReadOnlyList<SyncConflict> Conflicts()
    {
        var zig = Starter.First(gesture => gesture.Name == "Z");
        var mine = zig with { Name = "Zig" };
        var theirs = zig with { Name = "Zig", Samples = Starter.First(gesture => gesture.Name == "S").Samples };
        var chrome = Mapping().Groups[1];
        var close = new Command(CommandId.New(), "Close tab", Trigger.ForGesture(Starter[1].Id), IsActive: true, [new CommandStep(new DelayStep(30), HostPlatform.Windows)]);
        var closeThere = close with { Steps = [new CommandStep(new DelayStep(120), HostPlatform.MacOS)] };
        var media = new CommandCategory(CategoryId.New(), "Media");
        var space = HoldRemap.For(KeyCode.Space);
        var blender = Mapping().Groups[2];
        var game = new IgnoredApp(GroupId.New(), "Game", IsActive: true, AppMatcher.Empty with { WindowsProcessNames = ["game.exe"] }, DisableEntirely: true);
        return
        [
            Conflict(new SyncItem.GestureItem(mine), new SyncItem.GestureItem(theirs)),
            Conflict(new SyncItem.CommandItem(chrome.Id, close), new SyncItem.CommandItem(chrome.Id, closeThere)),
            Conflict(new SyncItem.GroupItem(chrome), new SyncItem.GroupItem(chrome with { Name = "Google Chrome", SuppressGlobals = true })),
            Conflict(new SyncItem.CategoryItem(GroupId.Global, media), null),
            Conflict(new SyncItem.HoldRemapItem(blender.Id, space), new SyncItem.HoldRemapItem(blender.Id, space with { TapTimeMs = 220 })),
            Conflict(null, new SyncItem.IgnoredItem(game)),
        ];
    }

    private static SyncConflict Conflict(SyncItem? mine, SyncItem? theirs)
    {
        var item = mine ?? theirs!;
        return new SyncConflict(item.Key, item.Name, mine?.Content, theirs?.Content) { MachineId = Guid.NewGuid(), MachineName = "Mac" };
    }
}
#endif
