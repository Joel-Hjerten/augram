using Augram.App.Components.CommandTree;
using Augram.App.Components.SectionForm;
using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class StepListTests
{
    [AvaloniaFact]
    public void ShowsOneRowPerStepAndExpandsTheSelectedOneWithItsForm()
    {
        var (list, _, _) = Show(selected: 1);

        Assert.Equal(["1.", "2.", "3."], list.Rows.Select(row => row.IndexText));
        Assert.Equal(["Center window", "Wait 50 ms", "Play/pause"], list.Rows.Select(row => row.SummaryText));
        Assert.Equal(["Window", "Delay", "Media key"], list.Rows.Select(row => row.TypeText));
        Assert.Equal("has macOS override", list.Rows[1].MarkerText);
        Assert.False(list.Rows[0].HasMarker);
        Assert.Contains(":inactive", list.Rows[2].Classes);

        Assert.True(list.Rows[1].IsExpanded);
        Assert.IsType<SectionForm>(list.Rows[1].Form);
        Assert.Null(list.Rows[0].Form);
        Assert.Null(list.Rows[2].Form);
        Assert.Same(list.Rows[1], list.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem);
    }

    [AvaloniaFact]
    public void AnEditRaisesEditAndTheFormSurvivesTheStoreEchoButNotAnUndo()
    {
        var (list, actions, steps) = Show(selected: 2);
        var form = list.Rows[2].Form!;
        var key = form.GetVisualDescendants().OfType<ComboBox>().Single();

        key.SelectedIndex = (int)MediaKeyKind.NextTrack;

        var edit = Assert.Single(actions);
        Assert.Equal(StepListAction.Edit, edit.Action);
        Assert.Equal(2, edit.Step!.Index);
        Assert.Equal(new MediaKeyStep(MediaKeyKind.NextTrack), edit.Edited);

        list.Steps = Replace(steps, 2, new MediaKeyStep(MediaKeyKind.NextTrack));
        Assert.Same(form, list.Rows[2].Form);
        Assert.Equal("Next track", list.Rows[2].SummaryText);

        list.Steps = Replace(steps, 2, new MediaKeyStep(MediaKeyKind.Stop));
        Assert.NotSame(form, list.Rows[2].Form);
        list.UpdateLayout();
        Assert.Equal("Stop", list.Rows[2].Form!.GetVisualDescendants().OfType<ComboBox>().Single().SelectedItem);
    }

    [AvaloniaFact]
    public void TypePickerOffersEveryCategoryButOtherAndAChoiceRaisesAdd()
    {
        var (list, actions, _) = Show();

        var offered = list.TypePicker.Offered;
        Assert.Equal(["windowOp", "mediaKey", "delay"], offered.Select(type => type.Key));
        Assert.DoesNotContain(offered, type => type.Category == StepCategory.Other);
        Assert.Equal(["System", "Timing"], list.TypePicker.Entries.OfType<TextBlock>().Select(text => text.Text));

        list.TypePicker.Choose(DelayStepType.Instance);

        var add = Assert.Single(actions);
        Assert.Equal(StepListAction.Add, add.Action);
        Assert.Same(DelayStepType.Instance, add.Type);
        Assert.Null(add.Step);
    }

    [AvaloniaFact]
    public void KeysAndTheCheckBoxRaiseTheirActionsOnTheSelectedStep()
    {
        var (list, actions, _) = Show(selected: 1);
        var window = (Window)TopLevel.GetTopLevel(list)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        var box = list.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(box.ContainerFromItem(box.SelectedItem!)!.Focus());

        window.KeyPressQwerty(PhysicalKey.D, modifier);
        window.KeyPressQwerty(PhysicalKey.C, modifier);
        window.KeyPressQwerty(PhysicalKey.V, modifier);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Z, modifier);
        list.Rows[1].GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = false;

        Assert.Equal([StepListAction.Duplicate, StepListAction.Copy, StepListAction.Paste, StepListAction.Delete, StepListAction.Undo, StepListAction.ToggleActive], actions.Select(action => action.Action));
        Assert.Equal(1, actions[0].Step!.Index);
        Assert.Null(actions[2].Step);
        Assert.Equal(1, actions[5].Step!.Index);
    }

    [AvaloniaFact]
    public void KeyBindingsStandDownWhileTheExpandedFormHasFocus()
    {
        var (list, actions, _) = Show(selected: 1);
        var window = (Window)TopLevel.GetTopLevel(list)!;
        var editor = list.Rows[1].Form!.GetVisualDescendants().OfType<NumericUpDown>().Single();
        Assert.True(editor.GetVisualDescendants().OfType<TextBox>().Single().Focus());
        Assert.True(list.IsEditing);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.D, CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control);

        Assert.Empty(actions);
    }

    [AvaloniaFact]
    public void ALeftDragPastTheThresholdReordersOntoTheRowUnderThePointer()
    {
        var (list, actions, _) = Show(selected: 0);
        var window = (Window)TopLevel.GetTopLevel(list)!;
        var from = Centre(list.Rows[0], window);
        var to = Centre(list.Rows[2], window);

        window.MouseDown(from, Avalonia.Input.MouseButton.Left);
        window.MouseMove(new Point(from.X, from.Y + 2));
        Assert.DoesNotContain(":dragging", list.Rows[0].Classes);
        window.MouseMove(to);
        Assert.Contains(":dragging", list.Rows[0].Classes);
        Assert.Contains(":drop-target", list.Rows[2].Classes);
        window.MouseUp(to, Avalonia.Input.MouseButton.Left);

        var reorder = Assert.Single(actions, action => action.Action == StepListAction.Reorder);
        Assert.Equal(0, reorder.Step!.Index);
        Assert.Equal(2, reorder.TargetIndex);
        Assert.DoesNotContain(":dragging", list.Rows[0].Classes);
        Assert.DoesNotContain(":drop-target", list.Rows[2].Classes);
    }

    private static Point Centre(Control row, Window window)
        => row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

    private static IReadOnlyList<StepItem> Replace(IReadOnlyList<CommandStep> steps, int index, IStep step)
        => steps.Select((item, i) => StepItem.From(i == index ? item with { Step = step } : item, i)).ToList();

    private static (StepList List, List<StepListActionEventArgs> Actions, IReadOnlyList<CommandStep> Steps) Show(int selected = -1)
    {
        IReadOnlyList<CommandStep> steps =
        [
            new(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
            new(new DelayStep(50), HostPlatform.Windows, MacOsOverride: new DelayStep(120)),
            new(new MediaKeyStep(MediaKeyKind.PlayPause), HostPlatform.Windows, IsActive: false),
        ];
        var actions = new List<StepListActionEventArgs>();
        var list = new StepList { Steps = steps.Select(StepItem.From).ToList(), SelectedIndex = selected, StepTypes = StepRegistry.BuiltIn.All, HasCommand = true };
        list.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = list, Width = 700, Height = 700 };
        window.Show();
        return (list, actions, steps);
    }
}
