using Augram.App.Components.HotkeyCapture;
using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    [AvaloniaFact]
    public void EachModifierHasAnRToggleForItsRightHandKey_EnabledOnlyWhileTheModifierIsOn()
    {
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), _ => { });

        Assert.Equal("Ctrl+RAlt+F9", Box(form).DisplayText);
        Assert.Equal([true, true, false, false], Toggles(form));
        Assert.Equal([false, true, false, false], RightToggles(form).Select(toggle => toggle.IsChecked == true));
        Assert.Equal([true, true, false, false], RightToggles(form).Select(toggle => toggle.IsEnabled));
        Assert.All(RightToggles(form), toggle =>
        {
            Assert.Equal<object?>("R", toggle.Content);
            Assert.Equal<object?>("Right-hand key", ToolTip.GetTip(toggle));
        });
    }

    [AvaloniaFact]
    public void TheRToggleEmitsTheNormalisedStep()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9), changes.Add);

        RightToggles(form)[1].IsChecked = true;
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9, KeyModifiers.Alt), Assert.Single(changes));
        Assert.Equal("RAlt+F9", Box(form).DisplayText);

        // Turning the modifier off drops its right-hand bit and disables its R toggle.
        form.GetVisualDescendants().OfType<CheckBox>().ElementAt(1).IsChecked = false;
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.F9), changes[^1]);
        Assert.Equal((false, false), (RightToggles(form)[1].IsChecked == true, RightToggles(form)[1].IsEnabled));
        Assert.Equal("F9", Box(form).DisplayText);

        // The R toggle of a modifier that is off changes nothing and stays off.
        RightToggles(form)[0].IsChecked = true;
        Assert.Equal(2, changes.Count);
        Assert.False(RightToggles(form)[0].IsChecked == true);

        // Back on, the modifier starts plain.
        form.GetVisualDescendants().OfType<CheckBox>().ElementAt(1).IsChecked = true;
        Assert.Equal(new HotkeyStep(KeyModifiers.Alt, KeyCode.F9), changes[^1]);
        Assert.Equal(3, changes.Count);
    }

    [AvaloniaFact]
    public void ACapturedRightHandCombinationEmitsOnceAndRefreshesTheRToggles()
    {
        var changes = new List<IStep>();
        var capture = new FakeKeyCapture();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control, KeyCode.T), changes.Add);
        var box = Box(form);
        box.KeyCapture = capture;

        box.BeginCapture();
        capture.Press(KeyCode.RightControl);
        capture.Press(KeyCode.RightShift, KeyModifiers.Control);
        capture.Press(KeyCode.P, KeyModifiers.Control | KeyModifiers.Shift);
        box.Accept();

        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.P, KeyModifiers.Control | KeyModifiers.Shift), Assert.Single(changes));
        Assert.Equal([true, false, true, false], Toggles(form));
        Assert.Equal([true, false, true, false], RightToggles(form).Select(toggle => toggle.IsChecked == true));
        Assert.Equal("RCtrl+RShift+P", box.DisplayText);
    }

    [AvaloniaFact]
    public void AStepWithAStrayRightHandBitIsShownAndEditedNormalised()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control, KeyCode.T, KeyModifiers.Control | KeyModifiers.Alt), changes.Add);

        Assert.Equal([true, false, false, false], RightToggles(form).Select(toggle => toggle.IsChecked == true));

        KeyDropdown(form).SelectedItem = "W";

        Assert.Equal(new HotkeyStep(KeyModifiers.Control, KeyCode.W, KeyModifiers.Control), Assert.Single(changes));
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

    private static ToggleButton[] RightToggles(Control form) => [.. form.GetVisualDescendants().OfType<ToggleButton>().Where(toggle => toggle.Name == "PART_Right")];
}
