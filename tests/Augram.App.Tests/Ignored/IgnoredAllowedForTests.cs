using Augram.App.Components.MasterDetail;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// "Allowed for" on an Exclusions › Global entry (Joel, 2026-10-10, plan 0005 decision 7): the commands whose Also in names it, as
/// links that open them, like a Per command entry's "Used by"; none in the disable-while-focused mode, where Core drops the entry
/// from their Also in (the message line says so); a move to Per command asks first while commands name it.
/// </summary>
public sealed class IgnoredAllowedForTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp Resolve = new(GroupId.New(), "DaVinci Resolve", IsActive: true, new AppMatcher { WindowsProcessNames = ["Resolve.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);

    [AvaloniaFact]
    public void AllowedForListsTheCommandsNamingItInTheirAlsoIn_EachALinkThatOpensIt_AndFollowsTheStore()
    {
        var (vm, store, _, locator) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var form = vm.Detail!;

        var allowed = AllowedFor(form);
        Assert.Equal(IgnoredEditViewModel.AllowedForNone, allowed.EmptyText);
        Assert.True(allowed.Visible!.Get());
        Assert.Equal([("Global › Media › Magnifier", null), ("Chrome › Back", "inactive")], allowed.Links.Get().Select(link => (link.Caption, link.Detail)));

        allowed.Links.Get()[0].Open!();
        Assert.Equal([Command(store, "Magnifier")], locator.Shown);

        // Unticking it in a command's Also in shows in the same form.
        store.UpdateCommand(GroupId.Global, Find(store, "Magnifier") with { AlsoIn = [] });
        Assert.Same(form, vm.Detail);
        Assert.Equal(["Chrome › Back"], allowed.Links.Get().Select(link => link.Caption));

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "DaVinci Resolve")));
        Assert.Empty(AllowedFor(vm.Detail!).Links.Get());
    }

    [AvaloniaFact]
    public void TheAllowedForLinksRender_AndHideInTheDisableWhileFocusedMode()
    {
        var (vm, _, _, _) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var form = new SectionForm { Screen = vm.Detail };
        new Window { Content = form, Width = 900, Height = 1600 }.Show();

        var row = form.GetVisualDescendants().OfType<FieldRow>().Single(candidate => candidate.Label == IgnoredEditViewModel.AllowedForLabel);
        Assert.Equal(["Global › Media › Magnifier", "Chrome › Back"], row.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("link")).Select(link => link.Content as string));
        Assert.True(row.IsVisible);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "VMware")));
        Assert.False(AllowedFor(vm.Detail!).Visible!.Get());
    }

    [AvaloniaFact]
    public void SwitchingToDisableWhileFocused_DropsItFromTheirAlsoIn_SaysSo_AndOneUndoBringsBoth()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var mode = Fields(vm.Detail!).OfType<ButtonRadioField<bool>>().Single();

        mode.Value.Set(true);

        Assert.True(store.FindIgnored(Blender.Id)!.DisableEntirely);
        Assert.Empty(Find(store, "Magnifier").AlsoIn);
        Assert.Empty(Find(store, "Back").AlsoIn);
        Assert.Equal($"'Blender' disables Augram while focused now, so it left the Also in of 2 commands. {Augram.App.Components.CommandTree.CommandsKeymap.Current.Undo} undoes it.", vm.Message);
        Assert.False(AllowedFor(vm.Detail!).Visible!.Get());

        store.Undo();
        Assert.False(store.FindIgnored(Blender.Id)!.DisableEntirely);
        Assert.Equal([Blender.Id], Find(store, "Magnifier").AlsoIn);
    }

    [AvaloniaFact]
    public void MoveToPerCommand_AsksWhileCommandsAreAllowedOverIt()
    {
        var (vm, store, confirm, _) = Create();

        confirm.Answer = false;
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(vm, "Blender")));
        Assert.Equal(
            ("Move to Per command", "'Blender' is allowed for 2 commands; on Per command it leaves their Also in, and Augram works over Blender again. Move?", "Move"),
            Assert.Single(confirm.Requests));
        Assert.False(store.FindIgnored(Blender.Id)!.IsPerCommand);

        confirm.Answer = true;
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(vm, "Blender")));
        Assert.True(store.FindIgnored(Blender.Id)!.IsPerCommand);
        Assert.Empty(Find(store, "Magnifier").AlsoIn);

        // An entry no command is allowed over moves without a question.
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Move, Item(vm, "DaVinci Resolve")));
        Assert.Equal(2, confirm.Requests.Count);
        Assert.True(store.FindIgnored(Resolve.Id)!.IsPerCommand);
    }

    /// <summary>
    /// Global's Media › Magnifier (Right + Left) and Chrome's inactive Back (Right + Middle), both Also in Blender; on Exclusions ›
    /// Global Blender and DaVinci Resolve in the plain mode and VMware disabling Augram while focused.
    /// </summary>
    private static (IgnoredViewModel Vm, MappingStore Store, FakeConfirmPresenter Confirm, FakeCommandLocator Locator) Create()
    {
        var media = new CommandCategory(CategoryId.New(), "Media");
        var right = new TriggerHold(HeldButtons.Right);
        var global = AppGroup.EmptyGlobal with
        {
            Categories = [media],
            Commands = [Cmd("Magnifier", Trigger.ForButton(MouseButton.Left, right)) with { CategoryId = media.Id, AlsoIn = [Blender.Id] }],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] },
            [Cmd("Back", Trigger.ForButton(MouseButton.Middle, right)) with { IsActive = false, AlsoIn = [Blender.Id] }]);
        var store = new MappingStore(new MappingDocument([global, chrome], [Blender, Resolve, VMware]));
        var confirm = new FakeConfirmPresenter();
        var locator = new FakeCommandLocator();
        return (new IgnoredViewModel(store, new FakeFormDialogPresenter(), confirm, HostPlatform.Windows, IgnoreScope.Global, locator), store, confirm, locator);
    }

    private static Command Cmd(string name, Trigger trigger)
        => new(CommandId.New(), name, trigger, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);

    private static CommandId Command(MappingStore store, string name) => Find(store, name).Id;

    private static Command Find(MappingStore store, string name) => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;

    private static MasterItem Item(IgnoredViewModel vm, string name) => vm.Items.Single(item => item.Name == name);

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);

    private static LinksField AllowedFor(FormScreen screen) => Fields(screen).OfType<LinksField>().Single(field => field.Label == IgnoredEditViewModel.AllowedForLabel);
}
