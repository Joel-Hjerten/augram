using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.ClearClipboard;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.ClearClipboard;
using Augram.Core.Steps.Scroll;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The Scroll step's form (direction, notches, held keys; every edit one new step) and the Clear clipboard step's note.</summary>
public sealed class ScrollStepFormTests
{
    [AvaloniaFact]
    public void TheFormShowsTheStep_WithTheKeysNamedAsThisKeyboardNamesThem()
    {
        var (form, _) = Show(new ScrollStep(ScrollDirection.Left, 4, KeyModifiers.Control | KeyModifiers.Shift), _ => { });

        Assert.Equal(["Direction", "Notches", "Keys held"], form.GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label));
        Assert.Equal("Left (sideways)", form.GetVisualDescendants().OfType<ComboBox>().Single().SelectedItem);
        Assert.Equal(4m, form.GetVisualDescendants().OfType<NumericUpDown>().Single().Value);
        var boxes = form.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(["Ctrl", "Alt", "Shift", "Win"], boxes.Select(box => box.Content));
        Assert.Equal([true, false, true, false], boxes.Select(box => box.IsChecked == true));
    }

    [AvaloniaFact]
    public void EveryFieldEmitsTheEditedStep()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new ScrollStep(ScrollDirection.Down), changes.Add);

        form.GetVisualDescendants().OfType<ComboBox>().Single().SelectedIndex = (int)ScrollDirection.Right;
        Assert.Equal(new ScrollStep(ScrollDirection.Right), Assert.Single(changes));

        form.GetVisualDescendants().OfType<NumericUpDown>().Single().Value = 3;
        Assert.Equal(new ScrollStep(ScrollDirection.Right, 3), changes[^1]);

        form.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = true;
        Assert.Equal(new ScrollStep(ScrollDirection.Right, 3, KeyModifiers.Control), changes[^1]);

        form.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = false;
        Assert.Equal(new ScrollStep(ScrollDirection.Right, 3), changes[^1]);
        Assert.Equal(4, changes.Count);
    }

    [AvaloniaFact]
    public void TheClearClipboardFormOnlySaysWhatItDoes()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new ClearClipboardStep(), changes.Add);

        Assert.Equal(["Does"], form.GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label));
        Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == ClearClipboardStepForm.WhatText);
        Assert.Empty(changes);
    }

    private static (Control Form, Window Window) Show(IStep step, Action<IStep> changed)
    {
        var form = StepFormRegistry.Default.Build(step, changed);
        var window = new Window { Content = form, Width = 700, Height = 500 };
        window.Show();
        return (form, window);
    }
}
