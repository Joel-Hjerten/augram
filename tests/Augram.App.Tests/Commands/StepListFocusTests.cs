using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.Screens;
using Augram.App.ViewModels.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using MouseButton = Avalonia.Input.MouseButton;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The keys follow a real click into the step list (Joel, 0.11.4: a press on a step's header or grip no longer lets the list
/// select on the press): after a click on a header, on the grip or a drag, Delete and Ctrl/Cmd+D act on the steps, never on
/// the command the tree had focus on. Through the workbench and the view model as the app wires them.
/// </summary>
public sealed class StepListFocusTests
{
    private static readonly RawInputModifiers Modifier = EditKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [AvaloniaFact]
    public void AClickOnAStepsHeaderTakesTheKeysFromTheTree()
    {
        var (vm, bench, window, tree, steps) = ShowThreeSteps();

        Click(window, StepPart(bench, 1, "PART_Name"));
        window.KeyPressQwerty(PhysicalKey.D, Modifier);

        Assert.Equal([StepListAction.Select, StepListAction.Duplicate], steps.Select(action => action.Action));
        Assert.Equal(1, steps[1].Step!.Index);
        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));

        FocusTree(bench);
        Click(window, StepPart(bench, 0, "PART_Name"));
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.Equal(StepListAction.Delete, steps[^1].Action);
        Assert.Equal(0, steps[^1].Step!.Index);
        Assert.Equal(["Wait 20 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Empty(tree);
    }

    [AvaloniaFact]
    public void AClickOnTheGripAndADragTakeTheKeysFromTheTreeToo()
    {
        var (vm, bench, window, tree, steps) = ShowThreeSteps();

        // Nothing open: the grip selects nothing, so Paste (which needs no step) shows where the keys went.
        Click(window, StepPart(bench, 0, "PART_Grip"));
        window.KeyPressQwerty(PhysicalKey.V, Modifier);
        Assert.Equal(StepListAction.Paste, Assert.Single(steps).Action);

        // The open step's grip leaves it open, and the keys act on it.
        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[2]));
        FocusTree(bench);
        Click(window, StepPart(bench, 2, "PART_Grip"));
        window.KeyPressQwerty(PhysicalKey.D, Modifier);
        Assert.Equal(StepListAction.Duplicate, steps[^1].Action);
        Assert.Equal(2, steps[^1].Step!.Index);
        Assert.Equal(3, vm.SelectedStepIndex);

        // A drag of another step by its header: the open copy stays open where it is, and Delete removes it.
        FocusTree(bench);
        Render();
        var from = Centre(StepPart(bench, 0, "PART_Name"), window);
        var to = Centre(bench.StepsPart!.Rows[1], window);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X, from.Y + 2));
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        Assert.Equal(StepListAction.Reorder, steps[^1].Action);
        Assert.Equal(["Wait 20 ms", "Wait 10 ms", "Wait 30 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(3, vm.SelectedStepIndex);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.Equal(StepListAction.Delete, steps[^1].Action);
        Assert.Equal(3, steps[^1].Step!.Index);
        Assert.Equal(["Wait 20 ms", "Wait 10 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Empty(tree);
    }

    /// <summary>The Global workbench with "Three steps" selected and keyboard focus on its row in the tree.</summary>
    private static (CommandsViewModel Vm, CommandsWorkbench Bench, Window Window, List<CommandTreeAction> Tree, List<StepListActionEventArgs> Steps) ShowThreeSteps()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Uncategorized")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized"), Item(vm, "Three steps")));
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandsScreen.Declare(vm)).Build());
        var window = new Window { Content = bench, Width = 1400, Height = 900 };
        window.Show();
        var tree = new List<CommandTreeAction>();
        var steps = new List<StepListActionEventArgs>();
        bench.TreeActionRequested += (_, e) => tree.Add(e.Action);
        bench.StepActionRequested += (_, e) => steps.Add(e);
        FocusTree(bench);

        // The tree has the keys to begin with (Copy reaches it, harmlessly), so a key that went there later would show.
        window.KeyPressQwerty(PhysicalKey.C, Modifier);
        Assert.Equal([CommandTreeAction.Copy], tree);
        Assert.Empty(steps);
        tree.Clear();
        return (vm, bench, window, tree, steps);
    }

    private static void FocusTree(CommandsWorkbench bench)
    {
        Render();
        var row = bench.TreePart!.Rows.OfType<CommandRow>().Single(candidate => candidate.NameText == "Three steps");
        Assert.True(row.FindAncestorOfType<ListBoxItem>()!.Focus());
        Assert.True(bench.TreePart.IsVisualAncestorOf((Visual)TopLevel.GetTopLevel(bench)!.FocusManager!.GetFocusedElement()!));
    }

    /// <summary>Joel, 0.14.4: a right-click aims the step's menu at it and opens or closes nothing.</summary>
    [AvaloniaFact]
    public void ARightClickOnAStepOpensAndClosesNothing_TheMenuActsOnIt()
    {
        var (vm, bench, window, _, steps) = ShowThreeSteps();
        Click(window, StepPart(bench, 0, "PART_Name"));
        Assert.Equal(0, vm.SelectedStepIndex);
        steps.Clear();

        Render();
        var point = Centre(StepPart(bench, 2, "PART_Name"), window);
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.DoesNotContain(steps, action => action.Action == StepListAction.Select);
        Assert.Equal(0, vm.SelectedStepIndex);
        Assert.Equal(2, bench.StepsPart!.SelectedStep!.Index);
    }

    private static Control StepPart(CommandsWorkbench bench, int index, string name)
        => bench.StepsPart!.Rows[index].GetVisualDescendants().OfType<Control>().Single(control => control.Name == name);

    /// <summary>Headless hit testing reads the last rendered frame, so lay out and render before every press.</summary>
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Click(Window window, Control target)
    {
        Render();
        var point = Centre(target, window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static Point Centre(Control control, Window window)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}
