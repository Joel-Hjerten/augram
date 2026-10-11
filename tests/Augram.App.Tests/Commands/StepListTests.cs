using System.Collections.ObjectModel;
using Augram.App.Components.CommandTree;
using Augram.App.Components.SectionForm;
using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using MouseButton = Avalonia.Input.MouseButton;

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
        Assert.False(list.Rows[1].HasMarker);
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

        // No refusals from the host: every type but the Other placeholders, Remap too (the host greys what a command cannot take).
        var offered = list.TypePicker.Offered;
        Assert.Equal(["windowOp", "mediaKey", "clearClipboard", "displayMode", "hdr", "hotkey", "scroll", "remap", "typeText", "run", "openApp", "delay"], offered.Select(type => type.Key));
        Assert.DoesNotContain(offered, type => type.Category == StepCategory.Other);
        Assert.Equal(["System", "Display", "Keyboard", "Mouse", "Text", "Run", "Timing"], list.TypePicker.Entries.OfType<TextBlock>().Select(text => text.Text));

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
    public void AClickOnAHeaderOpensItsStepAndASecondClickClosesItButAClickInTheFormDoesNot()
    {
        var (list, actions, _) = Show(echoSelect: true);
        var window = WindowOf(list);

        Click(window, () => Centre(Part(list.Rows[1], "PART_Name"), window));
        Assert.Equal(1, list.SelectedIndex);
        Assert.True(list.Rows[1].IsExpanded);
        Assert.IsType<SectionForm>(list.Rows[1].Form);

        // A field of the open form, and the form's own padding at its left edge (under the grip's column), keep it open.
        Click(window, () => Centre(list.Rows[1].Form!.GetVisualDescendants().OfType<NumericUpDown>().Single(), window));
        Click(window, () => Part(list.Rows[1], "PART_FormHost").TranslatePoint(new Point(2, 2), window)!.Value);
        Assert.True(list.Rows[1].IsExpanded);

        Click(window, () => Centre(Part(list.Rows[1], "PART_Name"), window));
        Assert.Equal(-1, list.SelectedIndex);
        Assert.False(list.Rows[1].IsExpanded);
        Assert.Null(list.Rows[1].Form);
        Assert.Null(list.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem);
        Assert.All(actions, action => Assert.Equal(StepListAction.Select, action.Action));
        Assert.Equal([1, -1], actions.Select(action => action.Step?.Index ?? -1));
    }

    [AvaloniaFact]
    public void AClickOnTheGripOrTheCardsEdgeBeforeItOpensAndClosesNothing()
    {
        var (list, actions, _) = Show(selected: 1, echoSelect: true);
        var window = WindowOf(list);

        Click(window, () => Centre(Part(list.Rows[0], "PART_Grip"), window));
        Click(window, () => Centre(Part(list.Rows[1], "PART_Grip"), window));
        Click(window, () => new Point(list.Rows[2].TranslatePoint(new Point(1, 0), window)!.Value.X, Centre(Part(list.Rows[2], "PART_Grip"), window).Y));

        Assert.Empty(actions);
        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal([false, true, false], list.Rows.Select(row => row.IsExpanded));
    }

    [AvaloniaFact]
    public void APressOnAClosedStepsHeaderThatMovesDragsItClosedAndTheDropReorders()
    {
        var (list, actions, _) = Show(echoSelect: true);
        var window = WindowOf(list);
        Render();
        var from = Centre(Part(list.Rows[0], "PART_Name"), window);
        var to = Centre(list.Rows[2], window);

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X, from.Y + 2));
        Assert.DoesNotContain(":dragging", list.Rows[0].Classes);
        window.MouseMove(to);
        Assert.Contains(":dragging", list.Rows[0].Classes);
        Assert.False(list.Rows[0].IsExpanded);

        // The order a release gives shows at once (Joel, 0.11.11): the dragged step follows the pointer down, the two it passed move up.
        Assert.True(list.Rows[0].DragOffset > 0);
        Assert.True(list.Rows[1].DragOffset < 0);
        Assert.Equal(list.Rows[1].DragOffset, list.Rows[2].DragOffset);
        Assert.Empty(actions);
        window.MouseUp(to, MouseButton.Left);

        var reorder = Assert.Single(actions);
        Assert.Equal(StepListAction.Reorder, reorder.Action);
        Assert.Equal(0, reorder.Step!.Index);
        Assert.Equal(2, reorder.TargetIndex);
        Assert.Equal(-1, list.SelectedIndex);
        Assert.DoesNotContain(":dragging", list.Rows[0].Classes);
        Assert.All(list.Rows, row => Assert.Equal(0, row.DragOffset));
    }

    [AvaloniaFact]
    public void ADragBroughtBackToItsPlaceMovesNothing_TheRowsGoBack()
    {
        var (list, actions, _) = Show(echoSelect: true);
        var window = WindowOf(list);
        Render();
        var from = Centre(Part(list.Rows[0], "PART_Name"), window);
        var to = Centre(list.Rows[2], window);

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        Assert.True(list.Rows[2].DragOffset < 0);
        window.MouseMove(from);
        Assert.All(list.Rows.Skip(1), row => Assert.Equal(0, row.DragOffset));
        window.MouseUp(from, MouseButton.Left);

        Assert.Empty(actions);
        Assert.All(list.Rows, row => Assert.Equal(0, row.DragOffset));
    }

    [AvaloniaFact]
    public void ADragByTheGripOrStartedOnTheOpenStepClosesItAndTheDropStillReorders()
    {
        var (list, actions, _) = Show(selected: 0, echoSelect: true);
        var window = WindowOf(list);
        Render();
        var from = Centre(Part(list.Rows[0], "PART_Grip"), window);

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X, from.Y + StepDragReorder.ThresholdPx + 2));
        Assert.Contains(":dragging", list.Rows[0].Classes);
        var close = Assert.Single(actions);
        Assert.Equal(StepListAction.Select, close.Action);
        Assert.Null(close.Step);
        Assert.Equal(-1, list.SelectedIndex);
        Assert.False(list.Rows[0].IsExpanded);
        Assert.Null(list.Rows[0].Form);

        // The rows moved up when the form closed; the order follows the pointer from there.
        Render();
        var to = Centre(list.Rows[2], window);
        window.MouseMove(to);
        Assert.True(list.Rows[2].DragOffset < 0);
        window.MouseUp(to, MouseButton.Left);

        Assert.Equal([StepListAction.Select, StepListAction.Reorder], actions.Select(action => action.Action));
        Assert.Equal(0, actions[1].Step!.Index);
        Assert.Equal(2, actions[1].TargetIndex);
        Assert.Equal(-1, list.SelectedIndex);
    }

    [AvaloniaFact]
    public void TheActiveBoxTogglesTheStepWithoutOpeningOrClosingAny()
    {
        var (list, actions, _) = Show(selected: 0, echoSelect: true);
        var window = WindowOf(list);

        Click(window, () => Centre(Part(list.Rows[1], "PART_Active"), window));
        Click(window, () => Centre(Part(list.Rows[0], "PART_Active"), window));

        Assert.Equal([StepListAction.ToggleActive, StepListAction.ToggleActive], actions.Select(action => action.Action));
        Assert.Equal([1, 0], actions.Select(action => action.Step!.Index));
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal([true, false, false], list.Rows.Select(row => row.IsExpanded));
    }

    /// <summary>
    /// "Change to…" (Joel, 2026-10-11): the menu's entry needs a selected step; it opens the step type picker at that step's header,
    /// and a type chosen there raises ChangeType for that step. The host's new step rebuilds the open form for its type.
    /// </summary>
    [AvaloniaFact]
    public void TheMenusChangeToNeedsASelectedStep_AndRaisesChangeTypeForItWithTheTypeChosen()
    {
        var (list, actions, steps) = Show();
        var box = list.GetVisualDescendants().OfType<ListBox>().Single();
        var menu = box.ContextMenu!;
        var entry = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, StepListAction.ChangeType));
        Assert.Equal("Change to…", entry.Header);
        Assert.Equal(1, menu.Items.IndexOf(entry));

        box.RaiseEvent(new ContextRequestedEventArgs());
        Assert.False(entry.IsEnabled);
        menu.Close();

        list.SelectedIndex = 1;
        box.RaiseEvent(new ContextRequestedEventArgs());
        Assert.True(entry.IsEnabled);
        menu.Close();
        entry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs(); // the picker opens once the menu has closed

        var picker = list.ChangeTypePicker;
        Assert.NotSame(list.TypePicker, picker);
        var popup = picker.GetLogicalAncestors().OfType<Popup>().First();
        Assert.True(popup.IsOpen);
        Assert.NotNull(list.Rows[1].HeaderPart);
        Assert.Same(list.Rows[1].HeaderPart, popup.PlacementTarget);
        Assert.Equal(12, picker.Offered.Count);

        picker.Choose(WindowOpStepType.Instance);

        Assert.False(popup.IsOpen);
        var change = Assert.Single(actions);
        Assert.Equal(StepListAction.ChangeType, change.Action);
        Assert.Equal(1, change.Step!.Index);
        Assert.Same(WindowOpStepType.Instance, change.Type);

        // The host puts a Window step in its place: the open step's form is rebuilt for it.
        var form = list.Rows[1].Form!;
        list.Steps = Replace(steps, 1, new WindowOpStep(WindowOperation.Minimize));
        Assert.NotSame(form, list.Rows[1].Form);
        list.UpdateLayout();
        Assert.Empty(list.Rows[1].Form!.GetVisualDescendants().OfType<NumericUpDown>());
        Assert.Equal("Window", list.Rows[1].TypeText);
    }

    /// <summary>The change picker greys what the host refuses for the step it changes (Core's, by step index), not "New step…"'s refusals.</summary>
    [AvaloniaFact]
    public void TheChangePickerGreysTheRefusalsOfTheStepItChanges()
    {
        var (list, actions, _) = Show(selected: 1);
        const string Already = "This step is Delay already.";
        var notHere = StepOffer.NotHere(RemapStepType.Instance);
        list.StepRefusals = new Dictionary<IStepType, string> { [RemapStepType.Instance] = notHere };
        list.ChangeRefusals =
        [
            new Dictionary<IStepType, string> { [WindowOpStepType.Instance] = "This step is Window already.", [RemapStepType.Instance] = notHere },
            new Dictionary<IStepType, string> { [DelayStepType.Instance] = Already, [RemapStepType.Instance] = notHere },
        ];

        list.OpenChangePicker(list.Steps[1]);

        var picker = list.ChangeTypePicker;
        Assert.Equal(10, picker.Offered.Count);
        Assert.DoesNotContain(DelayStepType.Instance, picker.Offered);
        Assert.Contains(WindowOpStepType.Instance, picker.Offered);
        var delay = picker.Entries.OfType<Button>().Single(button => button.Tag == DelayStepType.Instance);
        Assert.False(delay.IsEnabled);
        Assert.Equal(Already, ToolTip.GetTip(delay));
        Assert.Equal(11, list.TypePicker.Offered.Count);
        picker.Choose(DelayStepType.Instance);
        Assert.Empty(actions);

        // New refusals from the host follow while it is open; a step without an entry (the third) is offered every type.
        list.ChangeRefusals = [ReadOnlyDictionary<IStepType, string>.Empty, ReadOnlyDictionary<IStepType, string>.Empty];
        Assert.Equal(12, picker.Offered.Count);
        list.ChangeRefusals = [];
        list.OpenChangePicker(list.Steps[2]);
        Assert.Equal(12, picker.Offered.Count);
    }

    [Theory]
    [InlineData(0, 2, 0, 2)]
    [InlineData(0, 2, 1, 0)]
    [InlineData(0, 2, 2, 1)]
    [InlineData(2, 0, 0, 1)]
    [InlineData(2, 0, 1, 2)]
    [InlineData(2, 0, 2, 0)]
    [InlineData(0, 1, 2, 2)]
    [InlineData(1, 2, -1, -1)]
    public void AReorderMovesTheOpenStepWithItsStep(int from, int to, int open, int expected)
    {
        var step = StepItem.From(new CommandStep(new DelayStep(10), HostPlatform.Windows), from, HostPlatform.Windows);

        Assert.Equal(expected, new StepListActionEventArgs(StepListAction.Reorder, step, targetIndex: to).IndexAfterReorder(open));
    }

    private static Window WindowOf(StepList list) => (Window)TopLevel.GetTopLevel(list)!;

    private static Control Part(StepRow row, string name)
        => row.GetVisualDescendants().OfType<Control>().Single(control => control.Name == name);

    /// <summary>Headless hit testing reads the last rendered frame, so lay out and render before every press.</summary>
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>A left click at a point taken after rendering, so it sees the layout the previous click left.</summary>
    private static void Click(Window window, Func<Point> at)
    {
        Render();
        var point = at();
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static Point Centre(Control row, Window window)
        => row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

    private static IReadOnlyList<StepItem> Replace(IReadOnlyList<CommandStep> steps, int index, IStep step)
        => steps.Select((item, i) => StepItem.From(i == index ? item with { Step = step } : item, i, HostPlatform.Windows)).ToList();

    /// <summary>The three steps in a window, the one at <paramref name="selected"/> open; with <paramref name="echoSelect"/> the host answers Select as the view model does (the step opens; none closes the open one).</summary>
    private static (StepList List, List<StepListActionEventArgs> Actions, IReadOnlyList<CommandStep> Steps) Show(int selected = -1, bool echoSelect = false)
    {
        IReadOnlyList<CommandStep> steps =
        [
            new(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
            new(new DelayStep(50), HostPlatform.Windows),
            new(new MediaKeyStep(MediaKeyKind.PlayPause), HostPlatform.Windows, IsActive: false),
        ];
        var actions = new List<StepListActionEventArgs>();
        var list = new StepList { Steps = steps.Select((step, index) => StepItem.From(step, index, HostPlatform.Windows)).ToList(), SelectedIndex = selected, StepTypes = StepRegistry.BuiltIn.All, HasCommand = true };
        list.ActionRequested += (_, e) =>
        {
            actions.Add(e);
            if (echoSelect && e.Action == StepListAction.Select)
            {
                list.SelectedIndex = e.Step?.Index ?? -1;
            }
        };
        var window = new Window { Content = list, Width = 700, Height = 700 };
        window.Show();
        return (list, actions, steps);
    }
}
