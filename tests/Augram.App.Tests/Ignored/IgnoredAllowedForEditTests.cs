using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// "Allowed for" set from the excluded app's side (Joel, 2026-10-11: found where the app is): the row's Change… lists the
/// commands that can work over an excluded app (a trigger holding another button than the stroke button, none under a hold
/// remap), the allowed ones ticked; Save is one undo step and says so; Cancel stores nothing.
/// </summary>
public sealed class IgnoredAllowedForEditTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);

    [AvaloniaFact]
    public void Change_OffersTheCommandsThatCanWorkThere_TheAllowedOnesTicked()
    {
        var (vm, _, dialogs) = Create();
        dialogs.Answer = _ => false;

        ClickChange(vm);

        var list = Fields(dialogs.Last.Screen!).OfType<CheckListField>().Single();
        Assert.Equal(AllowedForEditViewModel.Title, dialogs.Last.Title);
        Assert.Equal(
            [("Global › Magnifier", "Right + Left", true), ("Global › Zoom In", "Right + wheel up", false), ("Chrome › Back", "Right + Middle · inactive", true)],
            list.Items.Select(item => (item.Caption, item.Detail, item.Value.Get())));
    }

    [AvaloniaFact]
    public void Save_StoresTheTicksAsOneUndoStep_AndSaysSo()
    {
        var (vm, store, dialogs) = Create();
        dialogs.Answer = request =>
        {
            var items = Fields(request.Screen!).OfType<CheckListField>().Single().Items;
            items.Single(item => item.Caption == "Global › Zoom In").Value.Set(true);
            items.Single(item => item.Caption == "Chrome › Back").Value.Set(false);
            return true;
        };

        ClickChange(vm);

        Assert.Equal([Blender.Id], Find(store, "Zoom In").AlsoIn);
        Assert.Empty(Find(store, "Back").AlsoIn);
        Assert.Equal([Blender.Id], Find(store, "Magnifier").AlsoIn);
        Assert.StartsWith("'Blender' is allowed for 2 commands now.", vm.Message, StringComparison.Ordinal);
        Assert.Equal(["Global › Magnifier", "Global › Zoom In"], AllowedFor(vm.Detail!).Links.Get().Select(link => link.Caption));

        Assert.True(store.Undo());
        Assert.Empty(Find(store, "Zoom In").AlsoIn);
        Assert.Equal([Blender.Id], Find(store, "Back").AlsoIn);
    }

    [AvaloniaFact]
    public void Cancel_OrNoChange_StoresNothing()
    {
        var (vm, store, dialogs) = Create();
        dialogs.Answer = _ => false;
        ClickChange(vm);
        dialogs.Answer = _ => true;
        ClickChange(vm);

        Assert.False(store.CanUndo);
        Assert.Null(vm.Message);
    }

    /// <summary>The selected Blender entry's Allowed for row: its Change… button, clicked.</summary>
    private static void ClickChange(IgnoredViewModel vm)
    {
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, vm.Items.Single(item => item.Name == "Blender")));
        var button = Assert.IsType<Button>(AllowedFor(vm.Detail!).Accessory!());
        Assert.Equal("Change…", button.Content);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>
    /// Global's Magnifier (Right + Left, Also in Blender), Zoom In (Right + wheel up) and Close (a gesture, which can never work
    /// over an excluded app), and Chrome's inactive Back (Right + Middle, Also in Blender); Blender on Exclusions › Global.
    /// </summary>
    private static (IgnoredViewModel Vm, MappingStore Store, FakeFormDialogPresenter Dialogs) Create()
    {
        var right = new TriggerHold(HeldButtons.Right);
        var global = AppGroup.EmptyGlobal with
        {
            Commands =
            [
                Cmd("Magnifier", Trigger.ForButton(MouseButton.Left, right)) with { AlsoIn = [Blender.Id] },
                Cmd("Zoom In", Trigger.ForWheel(WheelDirection.Up, right)),
                Cmd("Close", Trigger.ForGesture(GestureId.New())),
            ],
        };
        var chrome = new AppGroup(GroupId.New(), "Chrome", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["chrome.exe"] },
            [Cmd("Back", Trigger.ForButton(MouseButton.Middle, right)) with { IsActive = false, AlsoIn = [Blender.Id] }]);
        var store = new MappingStore(new MappingDocument([global, chrome], [Blender]));
        var dialogs = new FakeFormDialogPresenter();
        return (new IgnoredViewModel(store, dialogs, new FakeConfirmPresenter(), HostPlatform.Windows, IgnoreScope.Global, new FakeCommandLocator()), store, dialogs);
    }

    private static Command Cmd(string name, Trigger trigger)
        => new(CommandId.New(), name, trigger, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);

    private static Command Find(MappingStore store, string name) => store.Current.AllCommands().Single(pair => pair.Command.Name == name).Command;

    private static IEnumerable<Field> Fields(FormScreen screen) => screen.Sections.SelectMany(section => section.Fields);

    private static LinksField AllowedFor(FormScreen screen) => Fields(screen).OfType<LinksField>().Single(field => field.Label == IgnoredEditViewModel.AllowedForLabel);
}
