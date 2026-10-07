using Augram.App.Components.HotkeyCapture;
using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The Hotkey step's form: the capture field and the hand editor show one value, and every real edit from either emits one new step.</summary>
public sealed class HotkeyStepFormTests
{
    [AvaloniaFact]
    public void TheRegistryFindsTheFormAndItShowsTheStep()
    {
        Assert.True(StepFormRegistry.Default.Supports(HotkeyStepType.Instance));

        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Meta, KeyCode.Left), _ => { });

        Assert.Equal("Ctrl+Win+Left", Box(form).DisplayText);
        Assert.Equal([true, false, false, true], Toggles(form));
        Assert.Equal("Left", KeyDropdown(form).SelectedItem);
        Assert.Equal(["Ctrl", "Alt", "Shift", "Win", "Key"], form.GetVisualDescendants().OfType<FieldRow>().Skip(1).Select(row => row.Label));
    }

    [AvaloniaFact]
    public void HandEditsEmitTheEditedStepAndRefreshTheField()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control, KeyCode.T), changes.Add);

        form.GetVisualDescendants().OfType<CheckBox>().ElementAt(2).IsChecked = true;
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T), Assert.Single(changes));
        Assert.Equal("Ctrl+Shift+T", Box(form).DisplayText);

        KeyDropdown(form).SelectedItem = "F5";
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.F5), changes[^1]);

        form.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = true;
        Assert.Equal(2, changes.Count);
    }

    [AvaloniaFact]
    public void ACapturedCombinationEmitsOnceAndRefreshesTheHandEditor()
    {
        var changes = new List<IStep>();
        var capture = new FakeKeyCapture();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control, KeyCode.T), changes.Add);
        var box = Box(form);
        box.KeyCapture = capture;

        box.BeginCapture();
        capture.Press(KeyCode.LeftAlt);
        capture.Press(KeyCode.F4, KeyModifiers.Alt);
        box.Accept();

        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F4), Assert.Single(changes));
        Assert.Equal([false, true, false, false], Toggles(form));
        Assert.Equal("F4", KeyDropdown(form).SelectedItem);

        box.Clear();

        Assert.Equal(HotkeyStep.Unset, changes[^1]);
        Assert.Equal(HotkeyCaptureBox.EmptyText, KeyDropdown(form).SelectedItem);
        Assert.Equal(2, changes.Count);
    }

    private static (Control Form, Window Window) Show(IStep step, Action<IStep> changed)
    {
        var form = StepFormRegistry.Default.Build(step, changed);
        var window = new Window { Width = 700, Height = 600 };
        window.Resources.MergedDictionaries.Add(HotkeyCaptureBoxTests.Theme());
        window.Content = form;
        window.Show();
        window.UpdateLayout();
        return (form, window);
    }

    private static HotkeyCaptureBox Box(Control form) => form.GetVisualDescendants().OfType<HotkeyCaptureBox>().Single();

    private static ComboBox KeyDropdown(Control form) => form.GetVisualDescendants().OfType<ComboBox>().Single();

    private static bool[] Toggles(Control form) => [.. form.GetVisualDescendants().OfType<CheckBox>().Select(box => box.IsChecked == true)];
}
