using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Components.Steps;
using Augram.App.Components.StepTypePicker;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using PhysicalKey = Avalonia.Input.PhysicalKey;
using RawInputModifiers = Avalonia.Input.RawInputModifiers;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The step list of a command with a Remap step (F9, plan 0002 step 4; Joel, 0.11.3): "New step…" greys the types the host
/// refuses, with the reason as their tooltip, and is disabled with the reason when it can add none; a Remap edit the host
/// refused (the store unchanged, the same steps handed back) rebuilds the form on the stored step.
/// </summary>
public sealed class StepListHoldRemapTests
{
    private const string OnlyStep = "A Remap step is a command's only step.";

    [AvaloniaFact]
    public void ThePickerGreysExactlyTheRefusedTypes_WithTheReasonAsTheirTooltip()
    {
        var (list, actions) = Show(new RemapOutput.Button(MouseButton.Middle));
        var notHere = StepOffer.NotHere(RemapStepType.Instance);

        list.StepRefusals = new Dictionary<IStepType, string> { [RemapStepType.Instance] = notHere };

        var picker = list.TypePicker;
        Assert.Equal(12, picker.Listed.Count);
        Assert.Equal(["scroll", "remap"], picker.Listed.Where(type => type.Category == StepCategory.Mouse).Select(type => type.Key));
        Assert.Equal(11, picker.Offered.Count);
        Assert.DoesNotContain(RemapStepType.Instance, picker.Offered);
        var buttons = picker.Entries.OfType<Button>().ToList();
        var remap = buttons.Single(button => button.Tag == RemapStepType.Instance);
        Assert.False(remap.IsEnabled);
        Assert.Equal(notHere, ToolTip.GetTip(remap));
        Assert.True(ToolTip.GetShowOnDisabled(remap));
        Assert.All(buttons.Where(button => button != remap), button => Assert.True(button.IsEnabled));
        Assert.All(buttons.Where(button => button != remap), button => Assert.Null(ToolTip.GetTip(button)));
        Assert.True(list.CanAddStep);

        // A refused type raises nothing, even asked directly; an offered one raises Add.
        picker.Choose(RemapStepType.Instance);
        Assert.Empty(actions);

        list.StepRefusals = new Dictionary<IStepType, string>();
        Assert.Equal(12, picker.Offered.Count);
        picker.Choose(RemapStepType.Instance);
        Assert.Same(RemapStepType.Instance, Assert.Single(actions).Type);
    }

    [AvaloniaFact]
    public void NewStepIsDisabled_WithTheReasonAsItsTooltip_WhenNoTypeCanBeAdded()
    {
        var (list, actions) = Show(new RemapOutput.Key(KeyCode.X));
        var button = list.GetVisualDescendants().OfType<Button>().Single(part => part.Name == "PART_New");
        Assert.True(button.IsEnabled);
        Assert.Null(ToolTip.GetTip(button));

        // What Core refuses for a command whose only step is a Remap step: every type, Remap included, for the same reason.
        list.StepRefusals = StepRegistry.BuiltIn.All.ToDictionary(type => type, _ => OnlyStep);

        Assert.False(list.CanAddStep);
        Assert.Equal(OnlyStep, list.NewStepRefusal);
        Assert.False(button.IsEnabled);
        Assert.Equal(OnlyStep, ToolTip.GetTip(button));
        Assert.True(ToolTip.GetShowOnDisabled(button));
        Assert.Empty(list.TypePicker.Offered);

        // Its key opens no picker either; one type offered again enables both.
        var window = (Window)TopLevel.GetTopLevel(list)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        var box = list.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(box.ContainerFromItem(box.SelectedItem!)!.Focus());
        window.KeyPressQwerty(PhysicalKey.N, modifier);
        Assert.Null(list.TypePicker.GetVisualRoot());

        list.StepRefusals = StepRegistry.BuiltIn.All.Where(type => type != DelayStepType.Instance).ToDictionary(type => type, _ => OnlyStep);
        Assert.True(button.IsEnabled);
        Assert.Null(ToolTip.GetTip(button));
        Assert.Equal([DelayStepType.Instance], list.TypePicker.Offered);
        window.KeyPressQwerty(PhysicalKey.N, modifier);
        Assert.NotNull(list.TypePicker.GetVisualRoot());
        Assert.Empty(actions);
    }

    [AvaloniaFact]
    public void TheMostCommonReasonNamesWhyNothingCanBeAdded_AndNoCommandSaysNothing()
    {
        var notHere = StepOffer.NotHere(RemapStepType.Instance);
        var refusals = StepRegistry.BuiltIn.All.ToDictionary(type => type, type => type == RemapStepType.Instance ? notHere : OnlyStep);

        Assert.Equal(OnlyStep, StepTypePicker.NothingOffered(StepRegistry.BuiltIn.All, refusals));
        Assert.Null(StepTypePicker.NothingOffered(StepRegistry.BuiltIn.All, new Dictionary<IStepType, string> { [RemapStepType.Instance] = notHere }));
        Assert.Null(StepTypePicker.NothingOffered([], refusals));

        var list = new StepList { StepTypes = StepRegistry.BuiltIn.All, StepRefusals = refusals, HasCommand = false };
        Assert.False(list.CanAddStep);
        Assert.Null(list.NewStepRefusal);
    }

    [AvaloniaFact]
    public void ARefusedEditRebuildsTheFormOnTheStoredStep()
    {
        var (list, actions) = Show(new RemapOutput.Button(MouseButton.Middle));
        var form = list.Rows[0].Form!;
        var output = form.GetVisualDescendants().OfType<ComboBox>().First();

        output.SelectedIndex = (int)RemapOutputKind.Wheel;
        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up)), Assert.Single(actions).Edited);

        // The host refused it: nothing stored changed, and it hands the same steps back.
        list.Steps = [.. list.Steps];

        Assert.NotSame(form, list.Rows[0].Form);
        list.UpdateLayout();
        Assert.Equal("Button", list.Rows[0].Form!.GetVisualDescendants().OfType<ComboBox>().First().SelectedItem);
    }

    /// <summary>Plan 0005: the form follows the command's trigger; a new one rebuilds the expanded form (a button trigger offers a key output only).</summary>
    [AvaloniaFact]
    public void ANewFormContextRebuildsTheExpandedForm()
    {
        var (list, _) = Show(new RemapOutput.Key(KeyCode.None));
        var form = list.Rows[0].Form!;
        Assert.Equal(["Button", "Key", "Wheel"], OutputChoices(form));

        list.FormContext = StepFormContext.For(Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right)));

        Assert.NotSame(form, list.Rows[0].Form);
        list.UpdateLayout();
        Assert.Equal(["Key"], OutputChoices(list.Rows[0].Form!));
    }

    private static IEnumerable<string> OutputChoices(Control form) => form.GetVisualDescendants().OfType<ComboBox>().First().ItemsSource!.Cast<string>();

    private static (StepList List, List<StepListActionEventArgs> Actions) Show(RemapOutput output)
    {
        var actions = new List<StepListActionEventArgs>();
        var steps = new List<StepItem> { StepItem.From(new CommandStep(new RemapStep(output), HostPlatform.Windows), 0, HostPlatform.Windows) };
        var list = new StepList { Steps = steps, SelectedIndex = 0, StepTypes = StepRegistry.BuiltIn.All, HasCommand = true };
        list.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = list, Width = 800, Height = 800 }.Show();
        return (list, actions);
    }
}
