using Augram.App.Components.CommandTree;
using Augram.App.Components.HotkeyCapture;
using Augram.App.Declarations;
using Augram.App.Tests.Steps;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The hold remap's form in the side panel (F9, plan 0002 step 4): name, hold key (the capture field in one-key mode, which
/// refuses modifiers with Core's reason), tap time, active, Use on; each edit one undo step, a refused one shown and put back.
/// </summary>
public sealed class HoldRemapFormTests
{
    [AvaloniaFact]
    public void SelectingAHoldRemapShowsItsForm_EachEditIsOneUndoStep_ChangesFromTheTreeSyncIn()
    {
        var (vm, store, _) = CreateBlender();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, HoldRemapSection(vm, "Space")));

        var form = vm.GroupForm!;
        Assert.Equal(HoldRemapEditViewModel.Title, form.Sections[0].Title);
        Assert.Equal(["Name", "Hold key", "Tap time (ms)", "Active", "Use on"], form.Sections[0].Fields.Select(field => field.Label));
        var tap = Field<NumberField>(form, "Tap time (ms)");
        Assert.Equal((0d, 2000d, 180d), (tap.Min, tap.Max, tap.Value.Get()));

        tap.Value.Set(220);
        Field<ToggleField>(form, "Active").Value.Set(false);

        Assert.Equal((220, false), (Space(store).TapTimeMs, Space(store).IsActive));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal((220, true), (Space(store).TapTimeMs, Space(store).IsActive));
        Assert.True(Field<ToggleField>(form, "Active").Value.Get());

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, HoldRemapSection(vm, "Space"), null, "Navigate"));
        Assert.Same(form, vm.GroupForm);
        Assert.Equal("Navigate", Field<TextField>(form, "Name").Value.Get());
    }

    [AvaloniaFact]
    public void TheHoldKeyIsOneKey_AModifierIsRefusedWithTheRule_TheNameFollowsTheKey()
    {
        var (vm, store, _) = CreateBlender();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewHoldRemap, Section(vm, "Blender")));
        var (box, capture) = ShowHoldKey(vm.GroupForm!);
        Assert.True(box.SingleKey);

        box.BeginCapture();
        capture.Press(KeyCode.LeftShift);

        Assert.Equal("Left Shift cannot be a hold key: Ctrl, Alt, Shift and Win are already held for triggers.", box.StatusText);
        Assert.Equal(HotkeyCaptureBox.PromptKeyText, box.DisplayText);

        capture.Press(KeyCode.S, KeyModifiers.Shift);
        Assert.Equal("S", box.DisplayText);
        box.Accept();

        var added = Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Id != Space(store).Id);
        Assert.Equal(("S", KeyCode.S), (added.Name, added.HoldKey));
        Assert.Equal("S", Field<TextField>(vm.GroupForm!, "Name").Value.Get());

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        var undone = Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Id == added.Id);
        Assert.Equal(("Hold remap", KeyCode.None), (undone.Name, undone.HoldKey));
    }

    [AvaloniaFact]
    public void AKeyTheGroupUsesOrNoPlatformIsRefusedWithTheRule_AndTheFormGoesBack()
    {
        var (vm, store, _) = CreateBlender();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewHoldRemap, Section(vm, "Blender")));
        var form = vm.GroupForm!;
        var (box, capture) = ShowHoldKey(form);
        box.BeginCapture();
        capture.Press(KeyCode.S);
        box.Accept();

        box.BeginCapture();
        capture.Press(KeyCode.Space);
        box.Accept();

        Assert.NotNull(vm.Message);
        var added = Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Name == "S");
        Assert.Equal(KeyCode.S, added.HoldKey);
        Assert.Equal(KeyCode.S, box.Key);
        Assert.Equal("S", Field<TextField>(form, "Name").Value.Get());

        // The view model runs as on Windows: unticking macOS keeps the hold remap listed (and its form open) here.
        var useOn = Field<TogglesField>(form, "Use on");
        useOn.Options[1].Value.Set(false);
        Assert.Equal(PlatformSet.Windows, Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Name == "S").UseOn);
        useOn.Options[0].Value.Set(false);

        Assert.Equal("Use 'S' on at least one platform.", vm.Message);
        Assert.True(useOn.Options[0].Value.Get());
        Assert.Equal(PlatformSet.Windows, Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Name == "S").UseOn);
    }

    private static T Field<T>(FormScreen form, string label)
        where T : Field
        => form.Sections.SelectMany(section => section.Fields).OfType<T>().Single(field => field.Label == label);

    /// <summary>The form's hold key field in a window, capturing through a fake: nothing is hooked.</summary>
    private static (HotkeyCaptureBox Box, FakeKeyCapture Capture) ShowHoldKey(FormScreen form)
    {
        var capture = new FakeKeyCapture();
        var box = (HotkeyCaptureBox)Field<CustomField>(form, "Hold key").Build();
        box.KeyCapture = capture;
        var window = new Window { Width = 600, Height = 300 };
        window.Resources.MergedDictionaries.Add(HotkeyCaptureBoxTests.Theme());
        window.Content = box;
        window.Show();
        return (box, capture);
    }
}
