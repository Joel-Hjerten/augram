#if DEBUG
using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps.Remap;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.App.ViewModels.Commands;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The gallery's Button trigger pages (gallery rule, plan 0005 step 5): Magnifier in the Global workbench with its Remap key, and the trigger's states.</summary>
public sealed class ButtonTriggerGalleryTests
{
    private const string OnlyStep = "A Remap step is a command's only step.";

    [AvaloniaFact]
    public void TheWorkbenchShowsMagnifierWithItsPressedButtonAndARemapKeyOnly()
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(ButtonTriggerGalleryPage.WorkbenchPage()).Build());
        new Window { Content = bench, Width = 1400, Height = 900 }.Show();

        Assert.Equal("Magnifier", bench.SelectedCommand!.Name);
        Assert.Equal("Right + Left", bench.SelectedCommand.TriggerText);
        var header = bench.HeaderPart!;
        Assert.True(header.IsButtonKind);
        Assert.Equal(0, header.PressedButtonIndex);
        Assert.True(header.ShowsDragDistance);
        Assert.False(bench.IsUnderHoldRemap);

        // Joel, 0.11.3: its only step is its Remap step, so it takes no other; every type is greyed and "New step…" with it.
        var steps = bench.StepsPart!;
        Assert.Contains(RemapStepType.Instance, steps.TypePicker.Listed);
        Assert.Empty(steps.TypePicker.Offered);
        Assert.All(steps.TypePicker.Listed, type => Assert.Equal(OnlyStep, bench.StepRefusals[type]));
        Assert.False(steps.CanAddStep);
        Assert.Equal(OnlyStep, steps.NewStepRefusal);

        var row = Assert.Single(steps.Rows);
        Assert.True(row.IsExpanded);
        var stepForm = row.Form!;
        Assert.Contains(stepForm.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == RemapStepForm.ButtonTriggerNote);
        Assert.Equal(["Key"], stepForm.GetVisualDescendants().OfType<ComboBox>().First().ItemsSource!.Cast<string>());
    }

    [AvaloniaFact]
    public void TheStatesPageShowsTheStoredChord_ADraftWithoutAButton_AndATakenChord()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(ButtonTriggerGalleryPage.Page()) };
        new Window { Content = form, Width = 1000, Height = 1400 }.Show();

        var headers = form.GetVisualDescendants().OfType<CommandHeader>().ToList();
        Assert.Equal(["Magnifier", "Magnifier", "Back"], headers.Select(header => header.NameText));
        Assert.All(headers, header => Assert.True(header.IsButtonKind));
        Assert.Equal(
            [null, "Not saved yet: tick a button to hold, as Left for Left + Right.", "Not saved yet: 'Magnifier' already uses Right + Left here. Change the button pressed, a button held or a key."],
            headers.Select(header => header.DraftNote));
        Assert.Equal([false, false, true], headers.Select(header => header.CanTakeTrigger));
        Assert.Equal([false, false, true], headers.Select(header => header.CanSwapTrigger));
        Assert.Equal([true, true, true], headers.Select(header => header.CanSetAlsoIn));
        Assert.Equal(["Blender", "Blender", CommandItem.NoneNotIn], headers.Select(header => header.AlsoInText));

        var dialogs = form.GetVisualDescendants().OfType<FormDialog>().ToList();
        Assert.Equal(2, dialogs.Count);
        var boxes = dialogs[0].GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(["A Plague Tale", "Blender", "DaVinci Resolve"], boxes.Select(box => box.GetVisualDescendants().OfType<TextBlock>().First().Text));
        Assert.Equal([false, true, false], boxes.Select(box => box.IsChecked == true));
        Assert.Empty(dialogs[1].GetVisualDescendants().OfType<CheckBox>());
        Assert.Contains(dialogs[1].GetVisualDescendants().OfType<TextBlock>(), text => text.Text == AlsoInEditViewModel.NoAppsText);
    }

    [AvaloniaFact]
    public void TheWorkbenchHeaderNamesBlenderInMagnifiersAlsoIn()
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(ButtonTriggerGalleryPage.WorkbenchPage()).Build());
        new Window { Content = bench, Width = 1400, Height = 900 }.Show();

        Assert.True(bench.HeaderPart!.CanSetAlsoIn);
        Assert.Equal("Blender", bench.HeaderPart.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_AlsoInText").Text);
    }
}
#endif
