using Augram.App.Components.HotkeyCapture;
using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.Remap;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using KeyModifiers = Augram.Core.Abstractions.KeyModifiers;
using MouseButton = Augram.Core.Capture.MouseButton;

namespace Augram.App.Tests.Steps;

/// <summary>
/// The Remap step's form (F9, plan 0002 step 4): the output kind with what each kind needs (button, key by capture or by hand,
/// wheel direction), the modifiers (a key's with right-hand toggles, a button's or a wheel's as one row) and the note; every
/// edit one new step; a kind change keeps the modifiers and brings back the last value of that kind.
/// </summary>
public sealed class RemapStepFormTests
{
    [AvaloniaFact]
    public void AButtonOutputShowsTheButtonItsModifiersAndTheNote()
    {
        var form = Show(new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)), _ => { });

        Assert.Equal(["Output", "Button", "Keys held", "Note"], VisibleLabels(form));
        Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == RemapStepForm.Note);
        Assert.Equal(["Button", "Middle"], form.GetVisualDescendants().OfType<ComboBox>().Where(IsShown).Select(combo => (string)combo.SelectedItem!));
        var keys = form.GetVisualDescendants().OfType<CheckBox>().Where(IsShown).ToList();
        Assert.Equal(["Ctrl", "Alt", "Shift", "Win"], keys.Select(box => box.Content));
        Assert.Equal([false, false, true, false], keys.Select(box => box.IsChecked == true));
    }

    [AvaloniaFact]
    public void EachEditEmitsOneStep_AKindChangeKeepsTheModifiersAndTheLastValueOfThatKind()
    {
        var changes = new List<IStep>();
        var form = Show(new RemapStep(new RemapOutput.Button(MouseButton.Middle)), changes.Add);

        Combo(form, "Button").SelectedIndex = 1;
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Right)), Assert.Single(changes));

        form.GetVisualDescendants().OfType<CheckBox>().Where(IsShown).First().IsChecked = true;
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Right, KeyModifiers.Control)), changes[^1]);

        Combo(form, "Output").SelectedIndex = (int)RemapOutputKind.Wheel;
        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Control)), changes[^1]);
        Assert.Equal(["Output", "Direction", "Keys held", "Note"], VisibleLabels(form));
        Combo(form, "Direction").SelectedIndex = 1;
        Assert.Equal(new RemapStep(new RemapOutput.Wheel(ScrollDirection.Down, KeyModifiers.Control)), changes[^1]);

        Combo(form, "Output").SelectedIndex = (int)RemapOutputKind.Button;
        Assert.Equal(new RemapStep(new RemapOutput.Button(MouseButton.Right, KeyModifiers.Control)), changes[^1]);
        Assert.Equal(5, changes.Count);
    }

    [AvaloniaFact]
    public void AKeyOutputIsCapturedWithItsModifiersAndTheirSides()
    {
        var changes = new List<IStep>();
        var form = Show(new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift)), changes.Add);

        Combo(form, "Output").SelectedIndex = (int)RemapOutputKind.Key;

        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None, KeyModifiers.Shift)), changes[^1]);
        Assert.Equal(["Output", "Keys", "Key", "Ctrl", "Alt", "Shift", "Win", "Note"], VisibleLabels(form));
        form.UpdateLayout();
        Assert.True(form.GetVisualDescendants().OfType<ModifierToggle>().Single(toggle => toggle.IsOn).IsEffectivelyVisible);

        var capture = new FakeKeyCapture();
        var box = form.GetVisualDescendants().OfType<HotkeyCaptureBox>().Single();
        box.KeyCapture = capture;
        box.BeginCapture();
        capture.Press(KeyCode.LeftControl);
        capture.Press(KeyCode.RightAlt);
        capture.Press(KeyCode.R, KeyModifiers.Control | KeyModifiers.Alt);
        box.Accept();

        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.R, KeyModifiers.Control | KeyModifiers.Alt, KeyModifiers.Alt)), changes[^1]);
        Assert.Equal("R", (string)Combo(form, "Key").SelectedItem!);
        Assert.Equal([true, true, false, false], form.GetVisualDescendants().OfType<ModifierToggle>().Select(toggle => toggle.IsOn));
    }

    private static Control Show(IStep step, Action<IStep> changed)
    {
        var form = StepFormRegistry.Default.Build(step, changed);
        var window = new Window { Width = 800, Height = 700 };
        window.Resources.MergedDictionaries.Add(HotkeyCaptureBoxTests.Theme());
        window.Content = form;
        window.Show();
        return form;
    }

    private static List<string> VisibleLabels(Control form)
        => [.. form.GetVisualDescendants().OfType<FieldRow>().Where(row => row.IsVisible).Select(row => row.Label)];

    private static bool IsShown(Control control) => control.IsEffectivelyVisible;

    /// <summary>A row shown only after a kind change gets its template at the next layout pass.</summary>
    private static ComboBox Combo(Control form, string label)
    {
        form.UpdateLayout();
        return form.GetVisualDescendants().OfType<FieldRow>().Single(row => row.Label == label).GetVisualDescendants().OfType<ComboBox>().Single();
    }
}
