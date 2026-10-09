using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Ignored;

/// <summary>The Ignored tab's view model: the list with mode and matches, the selected app's form applying each edit, new, rename, active, delete with confirmation, undo.</summary>
public sealed class IgnoredViewModelTests
{
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, new AppMatcher { WindowsProcessNames = ["blender.exe"] }, DisableEntirely: false);
    private static readonly IgnoredApp Spine = new(GroupId.New(), "Spine", IsActive: false, new AppMatcher { Title = "Spine" }, DisableEntirely: false);

    [AvaloniaFact]
    public void TheListIsSortedByName_WithTheModeAndWhatItMatchesHere()
    {
        var (vm, _, _, _) = Create();

        Assert.Equal(("Ignored apps", "New ignored app"), (vm.Heading, vm.NewLabel));
        Assert.Equal(["Blender", "Spine", "VMware"], vm.Items.Select(item => item.Name));
        Assert.Equal(
            ["Gestures off over this app · blender.exe", "Gestures off over this app · Spine", "Disable while focused · vmware.exe"],
            vm.Items.Select(item => item.Summary));
        Assert.Equal([true, false, true], vm.Items.Select(item => item.IsActive));
        Assert.Null(vm.SelectedId);
        Assert.Null(vm.Detail);
    }

    [AvaloniaFact]
    public void OnAMac_TheSummarySaysWhatMatchesThere()
    {
        var (vm, _, _, _) = Create(HostPlatform.MacOS);

        Assert.Equal(
            ["Gestures off over this app · Blender (guessed)", "Gestures off over this app · Spine", "Disable while focused · nothing on macOS"],
            vm.Items.Select(item => item.Summary));
    }

    [AvaloniaFact]
    public void SelectingShowsTheSharedForm_AndEachEditAppliesAtOnce()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var form = vm.Detail!;

        Assert.Equal(Blender.Id.Value, vm.SelectedId);
        Assert.Equal(["Ignored app", "App identification", "More matching options"], form.Sections.Select(section => section.Title));
        Assert.Equal("blender.exe", Text("Windows executables", form).Get());
        Assert.Equal("macOS: Blender (guessed)", Text("Guess for an empty list", form).Get());

        Text("Name", form).Set("Blender 4");
        Text("macOS executables", form).Set("Blender");
        Choice("Mode", form).Set(true);
        Toggle("Not when full screen", form).Set(true);

        Assert.Same(form, vm.Detail);
        var stored = store.FindIgnored(Blender.Id)!;
        Assert.Equal("Blender 4", stored.Name);
        Assert.Equal(["Blender"], stored.Matcher.MacProcessNames);
        Assert.True(stored.DisableEntirely);
        Assert.True(stored.Matcher.IgnoreWhenFullScreen);
        Assert.Equal("Disable while focused · blender.exe", Item(vm, "Blender 4").Summary);
        Assert.True(store.CanUndo);
    }

    [AvaloniaFact]
    public void ARefusedEdit_ShowsTheRule_AndTheFieldKeepsWhatWasTyped()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var form = vm.Detail!;

        Text("Window title", form).Set("(");
        Toggle("Title is a regular expression", form).Set(true);

        Assert.NotNull(vm.Message);
        Assert.Equal("(", Text("Window title", form).Get());
        Assert.False(store.FindIgnored(Blender.Id)!.Matcher.TitleIsRegex);
    }

    [AvaloniaFact]
    public void AChangeFromElsewhere_IsSyncedIntoTheOpenForm()
    {
        var (vm, store, _, _) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        var form = vm.Detail!;
        Text("Name", form).Set("Blender 4");

        store.Undo();

        Assert.Same(form, vm.Detail);
        Assert.Equal("Blender", Text("Name", form).Get());
        Assert.Equal("Blender", Item(vm, "Blender").Name);
    }

    [AvaloniaFact]
    public void NewGoesThroughTheDeclaredForm_AndSelectsTheNewApp()
    {
        var (vm, store, dialogs, _) = Create();
        dialogs.Answer = request =>
        {
            Text("Name", request.Screen!).Set("DaVinci Resolve");
            Text("Windows executables", request.Screen!).Set("Resolve.exe");
            return true;
        };

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.New));

        Assert.Equal(("New ignored app", "Create"), (dialogs.Last.Title, dialogs.Last.ConfirmLabel));
        var resolve = store.Current.Ignored.Single(app => app.Name == "DaVinci Resolve");
        Assert.Equal(["Resolve.exe"], resolve.Matcher.WindowsProcessNames);
        Assert.False(resolve.DisableEntirely);
        Assert.Equal(resolve.Id.Value, vm.SelectedId);
        Assert.NotNull(vm.Detail);

        dialogs.Answer = _ => true;
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.New));
        Assert.Equal("An ignored app needs a name.", vm.Message);
        Assert.Equal(4, store.Current.Ignored.Count);
    }

    [AvaloniaFact]
    public void RenameAndTheActiveBox_ChangeTheStore()
    {
        var (vm, store, _, _) = Create();

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Rename, Item(vm, "Spine"), "Spine 4.2"));
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.ToggleActive, Item(vm, "Spine 4.2")));

        var spine = store.FindIgnored(Spine.Id)!;
        Assert.Equal("Spine 4.2", spine.Name);
        Assert.True(spine.IsActive);
        Assert.True(Item(vm, "Spine 4.2").IsActive);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Rename, Item(vm, "Spine 4.2"), "  "));
        Assert.Equal("An ignored app needs a name.", vm.Message);
    }

    [AvaloniaFact]
    public void DeleteAsksFirst_AndUndoBringsItBack()
    {
        var (vm, store, _, confirm) = Create();
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "VMware")));

        confirm.Answer = false;
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Delete, Item(vm, "VMware")));
        Assert.NotNull(store.FindIgnored(VMware.Id));

        confirm.Answer = true;
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Delete, Item(vm, "VMware")));

        Assert.Equal(2, confirm.Requests.Count);
        Assert.Equal(("Delete ignored app", "Delete"), (confirm.Requests[^1].Title, confirm.Requests[^1].ConfirmLabel));
        Assert.Null(store.FindIgnored(VMware.Id));
        Assert.Null(vm.SelectedId);
        Assert.Null(vm.Detail);
        Assert.StartsWith("Deleted 'VMware'.", vm.Message, StringComparison.Ordinal);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Undo));
        Assert.NotNull(store.FindIgnored(VMware.Id));
        Assert.Equal(["Blender", "Spine", "VMware"], vm.Items.Select(item => item.Name));
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Redo));
        Assert.Null(store.FindIgnored(VMware.Id));
    }

    private static (IgnoredViewModel Vm, MappingStore Store, FakeFormDialogPresenter Dialogs, FakeConfirmPresenter Confirm) Create(HostPlatform platform = HostPlatform.Windows)
    {
        var store = new MappingStore(new MappingDocument([AppGroup.EmptyGlobal], [VMware, Blender, Spine]));
        var dialogs = new FakeFormDialogPresenter();
        var confirm = new FakeConfirmPresenter();
        return (new IgnoredViewModel(store, dialogs, confirm, platform), store, dialogs, confirm);
    }

    private static MasterItem Item(IgnoredViewModel vm, string name) => vm.Items.Single(item => item.Name == name);

    private static Field FieldOf(string label, FormScreen screen) => screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label);

    private static IValueBinding<string> Text(string label, FormScreen screen) => (IValueBinding<string>)FieldOf(label, screen).Binding!;

    private static IValueBinding<bool> Toggle(string label, FormScreen screen) => (IValueBinding<bool>)FieldOf(label, screen).Binding!;

    private static IValueBinding<bool> Choice(string label, FormScreen screen) => ((ButtonRadioField<bool>)FieldOf(label, screen)).Value;
}
