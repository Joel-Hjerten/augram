using Augram.App.Components.StepList;
using Augram.App.Components.Steps;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The step list of a command under a hold remap (F9, plan 0002 step 4): "New step…" offers the Remap step there and nowhere
/// else; a Remap edit the host refused (the store unchanged, the same steps handed back) rebuilds the form on the stored step.
/// </summary>
public sealed class StepListHoldRemapTests
{
    [AvaloniaFact]
    public void ThePickerOffersRemapOnlyUnderAHoldRemap()
    {
        var (list, _) = Show(new RemapOutput.Button(MouseButton.Middle));

        Assert.DoesNotContain(RemapStepType.Instance, list.TypePicker.Offered);

        list.UnderHoldRemap = true;
        Assert.Equal(["scroll", "remap"], list.TypePicker.Offered.Where(type => type.Category == StepCategory.Mouse).Select(type => type.Key));
        Assert.Equal(12, list.TypePicker.Offered.Count);

        list.UnderHoldRemap = false;
        Assert.DoesNotContain(RemapStepType.Instance, list.TypePicker.Offered);
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
