using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.TypeText;
using Augram.Core.Steps;
using Augram.Core.Steps.TypeText;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The Type text step's form: a multi-line text field and the method dropdown, each real edit one new step.</summary>
public sealed class TypeTextStepFormTests
{
    [AvaloniaFact]
    public void TheRegistryFindsTheFormAndItShowsTheStep()
    {
        Assert.True(StepFormRegistry.Default.Supports(TypeTextStepType.Instance));

        var (form, _) = Show(new TypeTextStep("fov 70\nsecond", TypeTextMethod.Keys), _ => { });

        Assert.Equal(["Text", "Method"], form.GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label));
        Assert.Equal("fov 70\nsecond", Editor(form).Text);
        Assert.Equal(TypeTextStepForm.KeysLabel, Method(form).SelectedItem);
        Assert.Equal([TypeTextStepForm.UnicodeLabel, TypeTextStepForm.KeysLabel], Method(form).Items.Cast<string>());
    }

    [AvaloniaFact]
    public void TheTextFieldIsMultiLine_EnterAddsALineFeedAndLongTextWraps()
    {
        var (form, _) = Show(TypeTextStep.Empty, _ => { });
        var editor = Editor(form);

        Assert.True(editor.AcceptsReturn);
        Assert.Equal("\n", editor.NewLine);
        Assert.Equal(TextWrapping.Wrap, editor.TextWrapping);
        Assert.Contains("multiline", editor.Classes);
        Assert.Equal(Token("Editor.MultilineMinHeight"), editor.MinHeight);
        Assert.Equal(Token("Editor.MultilineMaxHeight"), editor.MaxHeight);
    }

    [AvaloniaFact]
    public void TypingEnterInTheFieldEmitsALineBreak()
    {
        var changes = new List<IStep>();
        var (form, window) = Show(TypeTextStep.Empty, changes.Add);
        var editor = Editor(form);
        editor.Focus();

        window.KeyTextInput("ab");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyTextInput("c");

        Assert.Equal("ab\nc", editor.Text);
        Assert.Equal(new TypeTextStep("ab\nc"), changes[^1]);
    }

    [AvaloniaFact]
    public void TheFieldGrowsWithItsTextUpToTheMaximumHeight()
    {
        var (form, _) = Show(new TypeTextStep("one line"), _ => { });
        var editor = Editor(form);
        form.UpdateLayout();
        var shortHeight = editor.Bounds.Height;

        editor.Text = string.Join("\n", Enumerable.Range(1, 40).Select(n => $"line {n}"));
        form.UpdateLayout();

        Assert.True(shortHeight >= Token("Editor.MultilineMinHeight"), $"short text: {shortHeight}");
        Assert.True(editor.Bounds.Height > shortHeight, $"long text: {editor.Bounds.Height}, short: {shortHeight}");
        Assert.True(editor.Bounds.Height <= Token("Editor.MultilineMaxHeight"), $"long text: {editor.Bounds.Height}");
    }

    [AvaloniaFact]
    public void EditsEmitTheEditedStep_AndAnEditThatChangesNothingEmitsNothing()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new TypeTextStep("fov 70"), changes.Add);

        Editor(form).Text = "fov 75";
        Assert.Equal(new TypeTextStep("fov 75"), Assert.Single(changes));

        Method(form).SelectedIndex = 1;
        Assert.Equal(new TypeTextStep("fov 75", TypeTextMethod.Keys), changes[^1]);

        Editor(form).Text = "fov 75";
        Method(form).SelectedIndex = 1;
        Assert.Equal(2, changes.Count);
    }

    // The dropdown's template has a TextBox of its own (editable ComboBox); the field's editor is the one with the field class.
    private static TextBox Editor(Control form) => form.GetVisualDescendants().OfType<TextBox>().Single(box => box.Classes.Contains("field-editor"));

    private static ComboBox Method(Control form) => form.GetVisualDescendants().OfType<ComboBox>().Single();

    private static double Token(string key)
    {
        Assert.True(Application.Current!.TryFindResource(key, out var value), $"No theme token '{key}'.");
        return Assert.IsType<double>(value);
    }

    private static (Control Form, Window Window) Show(IStep step, Action<IStep> changed)
    {
        var form = StepFormRegistry.Default.Build(step, changed);
        var window = new Window { Content = form, Width = 600, Height = 400 };
        window.Show();
        return (form, window);
    }
}
