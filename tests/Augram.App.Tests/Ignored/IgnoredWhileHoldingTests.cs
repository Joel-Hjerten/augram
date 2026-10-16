#if DEBUG
using Augram.App.Components.Capture;
using Augram.App.Components.HeldInputList;
using Augram.App.Components.MasterDetail;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.App.Screens;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Steps;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;

namespace Augram.App.Tests.Ignored;

/// <summary>
/// Exclusions › While holding (plan 0012): the third list's entries, the "Ignore gestures while holding" card's recorder and
/// drop-down, its "Ignore for" switch, the read-only other side, both Copies and the hold-key warning on a row. Over the
/// gallery's store (Blender, Alt · Space · Win authored on Windows, a hold remap on Space; After Effects with its own macOS list).
/// </summary>
public sealed class IgnoredWhileHoldingTests
{
    [AvaloniaFact]
    public void TheThirdList_AddsRenamesAndDeletesEntries_AndMovesNone()
    {
        var store = IgnoredGalleryPages.WhileHoldingStore();
        var dialogs = new FakeFormDialogPresenter();
        var confirm = new FakeConfirmPresenter { Answer = true };
        var vm = new IgnoredViewModel(store, dialogs, confirm, HostPlatform.Windows, IgnoreScope.WhileHolding);
        dialogs.Answer = request =>
        {
            Text("Name", request.Screen!).Set("Softimage");
            return true;
        };

        Assert.Equal(("While holding apps", "New app", null), (vm.Heading, vm.NewLabel, vm.MoveLabel));
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.New));
        var softimage = store.Current.Ignored.Single(app => app.Name == "Softimage");
        Assert.True(softimage.IsWhileHolding);
        Assert.Equal(softimage.Id.Value, vm.SelectedId);
        Assert.True(vm.ShowsHeldInputs);
        Assert.StartsWith("Nothing to hold yet · ", Item(vm, "Softimage").Summary, StringComparison.Ordinal);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Rename, Item(vm, "Softimage"), "XSI"));
        Assert.Equal("XSI", store.FindIgnored(softimage.Id)!.Name);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Delete, Item(vm, "XSI")));
        Assert.Null(store.FindIgnored(softimage.Id));
        Assert.Equal(["After Effects", "Blender"], vm.Items.Select(item => item.Name));
        Assert.DoesNotContain(new IgnoredViewModel(store, dialogs, confirm, HostPlatform.Windows).Items, item => item.Name == "Blender");
    }

    [AvaloniaFact]
    public void RecordingAKey_AddsItsInput_AModifierAloneRecordsTheModifier()
    {
        var (vm, store) = Create();
        var list = ShowCard(vm);
        var capture = new FakeKeyCapture();
        list.BeginAdd();
        TopLevel.GetTopLevel(list)!.UpdateLayout();
        var box = list.PickerPart!.GetVisualDescendants().OfType<HotkeyCaptureBox>().Single();
        box.KeyCapture = capture;

        box.BeginCapture();
        capture.Press(KeyCode.LeftShift);
        box.Accept();

        Assert.Equal(new KeyOrButton.Modifier(KeyModifiers.Shift), Blender(store).Holding.For(HostPlatform.Windows)[^1].Input);
        Assert.False(list.IsAdding);
        Assert.Equal(3, vm.SelectedHeldInput);
    }

    [AvaloniaFact]
    public void TheDropDown_AddsAButton()
    {
        var (vm, store) = Create();
        var list = ShowCard(vm);
        list.BeginAdd();
        TopLevel.GetTopLevel(list)!.UpdateLayout();
        var pick = list.PickerPart!.GetVisualDescendants().OfType<ComboBox>().Single(box => box.Name == "PART_Pick");

        pick.SelectedIndex = 2;

        Assert.Equal(new KeyOrButton.Button(MouseButton.Middle), Blender(store).Holding.For(HostPlatform.Windows)[^1].Input);
        Assert.Equal("Middle", vm.HeldInputs[^1].Name);
    }

    [AvaloniaFact]
    public void TheSwitch_OpensOnThisPlatform_AndResetsOnAnotherSelection()
    {
        var (vm, _) = Create();
        Assert.Equal((HostPlatform.Windows, true), (vm.HeldInputSide!.Shown, vm.HeldInputSide.IsHere));

        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.ShowSide, platform: HostPlatform.MacOS));
        Assert.Equal((HostPlatform.MacOS, "Edit on macOS"), (vm.HeldInputSide!.Shown, vm.HeldInputSide.Note));
        Assert.Equal(["Alt → Opt", "Space", "Win"], vm.HeldInputs.Select(item => item.Name));
        Assert.Equal("from Windows · needs a macOS version", vm.HeldInputs[2].Marker);

        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "After Effects")));
        Assert.Equal(HostPlatform.Windows, vm.HeldInputSide!.Shown);
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, "Blender")));
        Assert.Equal(HostPlatform.Windows, vm.HeldInputSide!.Shown);
    }

    [AvaloniaFact]
    public void TheOtherSide_IsReadOnly_NoEditReachesTheStore()
    {
        var (vm, store) = Create();
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.ShowSide, platform: HostPlatform.MacOS));
        var before = store.Current;
        var first = vm.HeldInputs[0];

        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.Add, input: new KeyOrButton.Key(KeyCode.S)));
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.Delete, first));
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.Reorder, first, targetIndex: 2));
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.CopyTo, first));

        Assert.True(vm.HeldInputsReadOnly);
        Assert.Same(before, store.Current);
    }

    [AvaloniaFact]
    public void CopyFromTheOtherPlatform_WorksFromBothSides_AndOneUndoRestores()
    {
        var (vm, store) = Create("After Effects");
        var original = AfterEffects(store).Holding;
        Assert.Equal("Copy from macOS", vm.HeldInputSide!.CopyText);

        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.CopyFromOtherPlatform));
        Assert.Equal([new KeyOrButton.Key(KeyCode.S)], AfterEffects(store).Holding.For(HostPlatform.Windows).Select(row => row.Input));
        Assert.Null(AfterEffects(store).Holding.Own);
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.Undo));
        Assert.Equal(original, AfterEffects(store).Holding);

        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.ShowSide, platform: HostPlatform.MacOS));
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.CopyFromOtherPlatform));
        Assert.True(AfterEffects(store).Holding.IsInferredOn(HostPlatform.MacOS));
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.Undo));
        Assert.Equal(original, AfterEffects(store).Holding);
    }

    [AvaloniaFact]
    public void CopyToOnAPeekRow_AddsThatInputConverted()
    {
        var (vm, store) = Create("After Effects");
        var entry = AfterEffects(store);
        store.UpdateIgnored(entry with { Holding = entry.Holding with { Own = new PlatformVersion<WhileHoldingInput>(HostPlatform.MacOS, [new(new KeyOrButton.Modifier(KeyModifiers.Meta), HostPlatform.MacOS)]) } });
        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.ShowSide, platform: HostPlatform.MacOS));
        Assert.Equal("Copy to Windows", vm.HeldInputCopyToText);

        vm.HandleHeldInput(new HeldInputListActionEventArgs(HeldInputListAction.CopyTo, vm.HeldInputs[0]));

        Assert.Equal(
            [new KeyOrButton.Modifier(KeyModifiers.Alt), new KeyOrButton.Modifier(KeyModifiers.Control)],
            AfterEffects(store).Holding.For(HostPlatform.Windows).Select(row => row.Input));
    }

    [AvaloniaFact]
    public void AListedHoldKey_WarnsOnItsRow()
    {
        var (vm, _) = Create();

        Assert.Equal([null, "Space is the hold key of Blender › Space; while Space runs it never ignores.", null], vm.HeldInputs.Select(item => item.Warning));
    }

    private static (IgnoredViewModel Vm, MappingStore Store) Create(string selected = "Blender")
    {
        var store = IgnoredGalleryPages.WhileHoldingStore();
        var vm = new IgnoredViewModel(store, new FakeFormDialogPresenter(), new FakeConfirmPresenter(), HostPlatform.Windows, IgnoreScope.WhileHolding);
        vm.Handle(new MasterDetailActionEventArgs(MasterDetailAction.Select, Item(vm, selected)));
        return (vm, store);
    }

    /// <summary>The Exclusions › While holding screen in a window, its card wired to the view model as the app wires it.</summary>
    private static HeldInputList ShowCard(IgnoredViewModel vm)
    {
        var view = Assert.IsType<MasterDetail>(Assert.IsType<ComponentScreen>(IgnoredScreen.Declare(vm)).Build());
        new Window { Content = view, Width = 1200, Height = 1600 }.Show();
        return view.GetVisualDescendants().OfType<HeldInputList>().Single();
    }

    private static IgnoredApp Blender(MappingStore store) => store.Current.Ignored.Single(app => app.Name == "Blender");

    private static IgnoredApp AfterEffects(MappingStore store) => store.Current.Ignored.Single(app => app.Name == "After Effects");

    private static MasterItem Item(IgnoredViewModel vm, string name) => vm.Items.Single(item => item.Name == name);

    private static IValueBinding<string> Text(string label, FormScreen screen)
        => (IValueBinding<string>)screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;
}
#endif
