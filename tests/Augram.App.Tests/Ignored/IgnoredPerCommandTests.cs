using Augram.App.Components.MasterDetail;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// The Ignored tab's two lists (plan 0004 step 7): Global and Per command each show their own entries; a Per command entry has
/// no mode and shows "Used by", the commands naming it as links that open them; a new entry lands on the sub-tab it was made
/// on; the right-click menu moves an entry between the lists in one undo step, asking first when commands use it.
/// </summary>
public sealed class IgnoredPerCommandTests
{
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp Spine = PerCommand("Spine", "Spine.exe");
    private static readonly IgnoredApp Krita = PerCommand("Krita", "krita.exe") with { IsActive = false };

    [AvaloniaFact]
    public void EachSubTabListsItsOwnEntries()
    {
        var (global, perCommand, _, _, _, _) = Create();

        Assert.Equal(("Ignored apps", "New ignored app", "Move to Per command"), (global.Heading, global.NewLabel, global.MoveLabel));
        Assert.Equal(["Blender", "VMware"], global.Items.Select(item => item.Name));
        Assert.Equal(["Gestures off over this app · blender.exe", "Disable while focused · vmware.exe"], global.Items.Select(item => item.Summary));

        Assert.Equal(("Per command apps", "New app", "Move to Global"), (perCommand.Heading, perCommand.NewLabel, perCommand.MoveLabel));
        Assert.Equal(["Krita", "Spine"], perCommand.Items.Select(item => item.Name));
        Assert.Equal(["Not used yet · krita.exe", "Used by 2 commands · Spine.exe"], perCommand.Items.Select(item => item.Summary));
        Assert.Equal([false, true], perCommand.Items.Select(item => item.IsActive));
        Assert.Contains("change nothing on their own", perCommand.Help, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void UsedByListsTheCommandsNamingIt_EachALinkThatOpensIt_AndFollowsTheStore()
    {
        var (_, vm, store, _, _, locator) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Spine")));
        var form = vm.Detail!;

        Assert.Equal(["Per command app", Augram.App.ViewModels.AppMatcherEditViewModel.SectionTitle], form.Sections.Select(section => section.Title));
        Assert.DoesNotContain(Fields(form), field => field.Label == "Mode");
        var usedBy = UsedBy(form);
        Assert.Equal(IgnoredEditViewModel.UsedByNone, usedBy.EmptyText);
        Assert.Equal([("Global › Media › Zoom in", null), ("Chrome › Zoom", "inactive")], usedBy.Links.Get().Select(link => (link.Caption, link.Detail)));

        usedBy.Links.Get()[1].Open!();
        Assert.Equal([Command(store, "Zoom")], locator.Shown);

        // A command ticking it elsewhere shows in the same form.
        var close = Command(store, "Close window");
        store.UpdateCommand(GroupId.Global, Find(store, close) with { NotIn = [Spine.Id] });
        Assert.Same(form, vm.Detail);
        Assert.Equal(["Global › Window › Close window", "Global › Media › Zoom in", "Chrome › Zoom"], usedBy.Links.Get().Select(link => link.Caption));
        Assert.Equal("Used by 3 commands · Spine.exe", Item(vm, "Spine").Summary);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Krita")));
        Assert.Empty(UsedBy(vm.Detail!).Links.Get());
    }

    [AvaloniaFact]
    public void TheUsedByLinksRender_AndAClickOpensTheCommand()
    {
        var (_, vm, store, _, _, locator) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Spine")));
        var form = new SectionForm { Screen = vm.Detail };
        new Window { Content = form, Width = 900, Height = 1600 }.Show();

        var row = form.GetVisualDescendants().OfType<FieldRow>().Single(candidate => candidate.Label == IgnoredEditViewModel.UsedByLabel);
        var links = row.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("link")).ToList();
        Assert.Equal(["Global › Media › Zoom in", "Chrome › Zoom"], links.Select(link => link.Content as string));
        links[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal([Command(store, "Zoom in")], locator.Shown);
    }

    [AvaloniaFact]
    public void NewOnEachSubTab_MakesAnEntryOnThatList()
    {
        var (global, perCommand, store, dialogs, _, _) = Create();
        dialogs.Answer = request =>
        {
            Assert.DoesNotContain(Fields(request.Screen!), field => field.Label == "Mode");
            Text("Name", request.Screen!).Set("Eyeris");
            return true;
        };

        perCommand.Handle(new MasterDetailActionEventArgs(MasterDetailAction.New));

        Assert.Equal(("New app", "Create"), (dialogs.Last.Title, dialogs.Last.ConfirmLabel));
        var eyeris = store.Current.Ignored.Single(app => app.Name == "Eyeris");
        Assert.True(eyeris.IsPerCommand);
        Assert.Equal(eyeris.Id.Value, perCommand.SelectedId);
        Assert.DoesNotContain(global.Items, item => item.Name == "Eyeris");

        dialogs.Answer = request =>
        {
            Text("Name", request.Screen!).Set("Resolve");
            return true;
        };
        global.Handle(new MasterDetailActionEventArgs(MasterDetailAction.New));
        Assert.False(store.Current.Ignored.Single(app => app.Name == "Resolve").IsPerCommand);
    }

    [AvaloniaFact]
    public void MoveToPerCommand_IsOneUndoStep_AndDropsTheDisableMode()
    {
        var (global, perCommand, store, _, confirm, _) = Create();
        global.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(global, "VMware")));

        global.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(global, "VMware")));

        Assert.Empty(confirm.Requests);
        var moved = store.FindIgnored(VMware.Id)!;
        Assert.True(moved.IsPerCommand);
        Assert.False(moved.DisableEntirely);
        Assert.Equal(["Blender"], global.Items.Select(item => item.Name));
        Assert.Null(global.SelectedId);
        Assert.Contains("VMware", perCommand.Items.Select(item => item.Name));
        Assert.StartsWith("Moved 'VMware' to Ignored › Per command", global.Message, StringComparison.Ordinal);
        Assert.Contains("no longer disables Augram while focused", global.Message, StringComparison.Ordinal);

        global.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.Equal((IgnoreScope.Global, true), (store.FindIgnored(VMware.Id)!.Scope, store.FindIgnored(VMware.Id)!.DisableEntirely));
        Assert.Equal(["Blender", "VMware"], global.Items.Select(item => item.Name));
    }

    [AvaloniaFact]
    public void MoveToGlobal_AsksWhenCommandsUseIt_AndDropsItFromTheirNotIn_InOneUndoStep()
    {
        var (global, perCommand, store, _, confirm, _) = Create();

        confirm.Answer = false;
        perCommand.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(perCommand, "Spine")));
        var asked = Assert.Single(confirm.Requests);
        Assert.Equal(
            ("Move to Global", "'Spine' is used by 2 commands; on Global it stops all of Augram over Spine and leaves their Not in. Move?", "Move"),
            asked);
        Assert.True(store.FindIgnored(Spine.Id)!.IsPerCommand);

        confirm.Answer = true;
        perCommand.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(perCommand, "Spine")));
        Assert.False(store.FindIgnored(Spine.Id)!.IsPerCommand);
        Assert.Empty(Find(store, Command(store, "Zoom in")).NotIn);
        Assert.Empty(Find(store, Command(store, "Zoom")).NotIn);
        Assert.Contains("Spine", global.Items.Select(item => item.Name));

        store.Undo();
        Assert.True(store.FindIgnored(Spine.Id)!.IsPerCommand);
        Assert.Equal([Spine.Id], Find(store, Command(store, "Zoom in")).NotIn);

        // An entry no command names moves without a question.
        perCommand.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(perCommand, "Krita")));
        Assert.Equal(2, confirm.Requests.Count);
        Assert.False(store.FindIgnored(Krita.Id)!.IsPerCommand);
    }

    [AvaloniaFact]
    public void DeleteOnPerCommand_SaysWhichCommandsWorkOverItAgain()
    {
        var (_, perCommand, store, _, confirm, _) = Create();

        perCommand.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Delete, Item(perCommand, "Spine")));

        Assert.Equal(("Delete app", "Delete 'Spine' from Per command? The 2 commands naming it in their Not in work over it again.", "Delete"), confirm.Requests.Single());
        Assert.Null(store.FindIgnored(Spine.Id));
        Assert.Empty(Find(store, Command(store, "Zoom in")).NotIn);
    }

    /// <summary>
    /// Global (Media › Zoom in naming Spine, Window › Close window), Chrome (an inactive Zoom naming Spine); on the ignore list
    /// VMware and Blender on Global, Spine and an inactive Krita on Per command.
    /// </summary>
    private static (IgnoredViewModel Global, IgnoredViewModel PerCommand, MappingStore Store, FakeFormDialogPresenter Dialogs, FakeConfirmPresenter Confirm, FakeCommandLocator Locator) Create()
    {
        var media = new CommandCategory(CategoryId.New(), "Media");
        var window = new CommandCategory(CategoryId.New(), "Window");
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [media, window],
            Commands = [Cmd("Zoom in") with { CategoryId = media.Id, NotIn = [Spine.Id] }, Cmd("Close window") with { CategoryId = window.Id }],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] },
            [Cmd("Zoom") with { IsActive = false, NotIn = [Spine.Id] }]);
        var store = new MappingStore(new MappingDocument([global, chrome], [VMware, Blender, Spine, Krita]));
        var dialogs = new FakeFormDialogPresenter();
        var confirm = new FakeConfirmPresenter();
        var locator = new FakeCommandLocator();
        return (
            new IgnoredViewModel(store, dialogs, confirm, HostPlatform.Windows, IgnoreScope.Global, locator),
            new IgnoredViewModel(store, dialogs, confirm, HostPlatform.Windows, IgnoreScope.PerCommand, locator),
            store,
            dialogs,
            confirm,
            locator);
    }

    private static IgnoredApp PerCommand(string name, string executable)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [executable] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    private static Command Cmd(string name)
        => new(CommandId.New(), name, Trigger.None, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);

    private static CommandId Command(MappingStore store, string name) => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command.Id;

    private static Command Find(MappingStore store, CommandId id) => store.FindCommand(id)!.Value.Command;

    private static MasterItem Item(IgnoredViewModel vm, string name) => vm.Items.Single(item => item.Name == name);

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);

    private static LinksField UsedBy(FormScreen screen) => Fields(screen).OfType<LinksField>().Single(field => field.Label == IgnoredEditViewModel.UsedByLabel);

    private static IValueBinding<string> Text(string label, FormScreen screen) => (IValueBinding<string>)Fields(screen).Single(field => field.Label == label).Binding!;
}
