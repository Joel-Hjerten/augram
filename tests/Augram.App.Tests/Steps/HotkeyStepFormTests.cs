using Augram.App.Components;
using Augram.App.Components.Capture;
using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.Hotkey;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
        Assert.Equal(["Keys", HotkeyStepForm.PerLayoutLabel, "Ctrl", "Alt", "Shift", "Win", "Key"], Labels(form));
        Assert.False(PerLayoutSwitch(form).IsChecked);
    }

    [AvaloniaFact]
    public void PerLayoutShowsOneRowPerInstalledLayoutNamedByTheLayout()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), changes.Add, new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc));
        Assert.DoesNotContain("Swedish", Labels(form));

        // On: a row per installed layout, empty, and nothing emitted; the switch alone is not an edit.
        PerLayoutSwitch(form).IsChecked = true;

        Assert.Equal(["Keys", HotkeyStepForm.PerLayoutLabel, "Swedish", "ABC", "Ctrl", "Alt", "Shift", "Win", "Key"], Labels(form));
        Assert.Equal([HotkeyCaptureBox.EmptyText, HotkeyCaptureBox.EmptyText], LayoutBoxes(form).Select(box => box.DisplayText));
        Assert.Empty(changes);

        // A step opened with a layout shortcut opens with the switch on and the row filled.
        var (filled, _) = Show(ChromeBack, changes.Add, new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc));
        Assert.True(PerLayoutSwitch(filled).IsChecked);
        Assert.Equal(["Alt+Win+8", HotkeyCaptureBox.EmptyText], LayoutBoxes(filled).Select(box => box.DisplayText));
    }

    [AvaloniaFact]
    public void ACapturedLayoutRowEmitsAStepWithThatLayoutsShortcutAndTheMainOneUnchanged()
    {
        var changes = new List<IStep>();
        var capture = new FakeKeyCapture();
        var (form, _) = Show(ChromeBack, changes.Add, new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc));
        var abc = LayoutBoxes(form)[1];
        abc.KeyCapture = capture;

        abc.BeginCapture();
        capture.Press(KeyCode.LeftMeta);
        capture.Press(KeyCode.OpenBracket, KeyModifiers.Meta);
        abc.Accept();

        var expected = ChromeBack with { Layouts = new Dictionary<string, HotkeyBinding> { [FakeKeyboardLayouts.Swedish.Id] = SwedishBack, [FakeKeyboardLayouts.Abc.Id] = new(KeyModifiers.Meta, KeyCode.OpenBracket) } };
        Assert.Equal(expected, Assert.Single(changes));
        Assert.Equal("Win+[", Box(form).DisplayText);

        // The main field captured anew keeps the layout shortcuts.
        var main = Box(form);
        main.KeyCapture = capture;
        main.BeginCapture();
        capture.Press(KeyCode.LeftAlt);
        capture.Press(KeyCode.Left, KeyModifiers.Alt);
        main.Accept();

        Assert.Equal(expected with { Modifiers = KeyModifiers.Alt, Key = KeyCode.Left }, changes[^1]);
        Assert.Equal(2, changes.Count);
    }

    [AvaloniaFact]
    public void AnAuthoredButUninstalledLayoutShowsByItsId()
    {
        var (form, _) = Show(ChromeBack, _ => { }, new FakeKeyboardLayouts(FakeKeyboardLayouts.Abc));

        Assert.Equal(["Keys", HotkeyStepForm.PerLayoutLabel, "ABC", FakeKeyboardLayouts.Swedish.Id, "", "Ctrl", "Alt", "Shift", "Win", "Key"], Labels(form));
        Assert.Equal([HotkeyCaptureBox.EmptyText, "Alt+Win+8"], LayoutBoxes(form).Select(box => box.DisplayText));
        Assert.Equal<object?>(HotkeyStepForm.RemoveLabel, Assert.Single(RemoveButtons(form)).Content);
    }

    [AvaloniaFact]
    public void RemoveDropsAnUninstalledLayoutsShortcut()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(ChromeBack, changes.Add, new FakeKeyboardLayouts(FakeKeyboardLayouts.Abc));

        Assert.Single(RemoveButtons(form)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), Assert.Single(changes));
        Assert.Equal(["Keys", HotkeyStepForm.PerLayoutLabel, "ABC", "Ctrl", "Alt", "Shift", "Win", "Key"], Labels(form));
        Assert.True(PerLayoutSwitch(form).IsChecked);
    }

    [AvaloniaFact]
    public void TheSwitchOffDropsEveryLayoutShortcutInOneEdit()
    {
        var changes = new List<IStep>();
        var both = ChromeBack with { Layouts = new Dictionary<string, HotkeyBinding> { [FakeKeyboardLayouts.Swedish.Id] = SwedishBack, [FakeKeyboardLayouts.Abc.Id] = new(KeyModifiers.Meta, KeyCode.OpenBracket) } };
        var (form, _) = Show(both, changes.Add, new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc));

        PerLayoutSwitch(form).IsChecked = false;

        Assert.Equal(new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket), Assert.Single(changes));
        Assert.Equal(["Keys", HotkeyStepForm.PerLayoutLabel, "Ctrl", "Alt", "Shift", "Win", "Key"], Labels(form));
        Assert.Equal("Win+[", Box(form).DisplayText);
    }

    [AvaloniaFact]
    public void TheRowsFollowTheLayoutsWhenThePortsAreRepublished()
    {
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Abc);
        var (form, window) = Show(ChromeBack, _ => { }, layouts);
        Assert.Equal(["ABC", FakeKeyboardLayouts.Swedish.Id], LayoutLabels(form));

        // Swedish is installed now, and the composition root re-publishes the ports (the same instance) as it does on a layout change.
        layouts.Installed = [FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc];
        var ports = new ComponentPorts();
        window.Resources[ComponentPorts.ResourceKey] = ports;
        Assert.Equal(["Swedish", "ABC"], LayoutLabels(form));
        Assert.Equal(["Alt+Win+8", HotkeyCaptureBox.EmptyText], LayoutBoxes(form).Select(box => box.DisplayText));

        // The same reading again re-declares nothing.
        var before = Box(form);
        window.Resources[ComponentPorts.ResourceKey] = ports;
        Assert.Same(before, Box(form));
    }

    [AvaloniaFact]
    public void HandEditsEmitTheEditedStepAndRefreshTheField()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new HotkeyStep(KeyModifiers.Control, KeyCode.T), changes.Add);

        ModifierBoxes(form)[2].IsChecked = true;
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.T), Assert.Single(changes));
        Assert.Equal("Ctrl+Shift+T", Box(form).DisplayText);

        KeyDropdown(form).SelectedItem = "F5";
        Assert.Equal(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.F5), changes[^1]);

        ModifierBoxes(form)[0].IsChecked = true;
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
        ModifierBoxes(form)[1].IsChecked = false;
        Assert.Equal(new HotkeyStep(KeyModifiers.None, KeyCode.F9), changes[^1]);
        Assert.Equal((false, false), (RightToggles(form)[1].IsChecked == true, RightToggles(form)[1].IsEnabled));
        Assert.Equal("F9", Box(form).DisplayText);

        // The R toggle of a modifier that is off changes nothing and stays off.
        RightToggles(form)[0].IsChecked = true;
        Assert.Equal(2, changes.Count);
        Assert.False(RightToggles(form)[0].IsChecked == true);

        // Back on, the modifier starts plain.
        ModifierBoxes(form)[1].IsChecked = true;
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

    /// <summary>Chrome's Back: Cmd+[ (Win+[ under the tests' Windows names), and Cmd+Opt+8 under the Swedish layout.</summary>
    private static readonly HotkeyBinding SwedishBack = new(KeyModifiers.Meta | KeyModifiers.Alt, KeyCode.Digit8);

    private static readonly HotkeyStep ChromeBack = new HotkeyStep(KeyModifiers.Meta, KeyCode.OpenBracket) { Layouts = new Dictionary<string, HotkeyBinding> { [FakeKeyboardLayouts.Swedish.Id] = SwedishBack } };

    private static (Control Form, Window Window) Show(IStep step, Action<IStep> changed, FakeKeyboardLayouts? layouts = null)
    {
        var form = layouts is null ? StepFormRegistry.Default.Build(step, changed) : new HotkeyStepForm(layouts).Build(step, changed);
        var window = new Window { Width = 700, Height = 600 };
        window.Content = form;
        window.Show();
        window.UpdateLayout();
        return (form, window);
    }

    private static HotkeyCaptureBox Box(Control form) => Laid(form).GetVisualDescendants().OfType<HotkeyCaptureBox>().First();

    /// <summary>The per-layout rows' fields, in row order (every capture field after the main one).</summary>
    private static HotkeyCaptureBox[] LayoutBoxes(Control form) => [.. Laid(form).GetVisualDescendants().OfType<HotkeyCaptureBox>().Skip(1)];

    private static string[] Labels(Control form) => [.. Laid(form).GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label)];

    /// <summary>The form after a layout pass: a re-declared screen (the switch, Remove, a layout change) has its rows only once laid out.</summary>
    private static Control Laid(Control form)
    {
        form.UpdateLayout();
        return form;
    }

    private static string[] LayoutLabels(Control form) => [.. Labels(form).Skip(2).SkipLast(5).Where(label => label.Length > 0)];

    private static CheckBox PerLayoutSwitch(Control form) => (CheckBox)Laid(form).GetVisualDescendants().OfType<FieldRow>().Single(row => row.Label == HotkeyStepForm.PerLayoutLabel).Editor!;

    private static Button[] RemoveButtons(Control form) => [.. Laid(form).GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, HotkeyStepForm.RemoveLabel))];

    private static ComboBox KeyDropdown(Control form) => form.GetVisualDescendants().OfType<ComboBox>().Single();

    private static bool[] Toggles(Control form) => [.. ModifierBoxes(form).Select(box => box.IsChecked == true)];

    /// <summary>The hand editor's four modifier check boxes (not the Per keyboard layout switch).</summary>
    private static CheckBox[] ModifierBoxes(Control form) => [.. form.GetVisualDescendants().OfType<ModifierToggle>().SelectMany(toggle => toggle.GetVisualDescendants().OfType<CheckBox>())];

    private static ToggleButton[] RightToggles(Control form) => [.. form.GetVisualDescendants().OfType<ToggleButton>().Where(toggle => toggle.Name == "PART_Right")];
}
