using Augram.App.Components.SectionForm;
using Augram.App.Components.Steps;
using Augram.App.Components.Steps.Run;
using Augram.Core.Steps;
using Augram.Core.Steps.Run;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Steps;

/// <summary>The Run step's form: it shows the step, every field emits one new step, and "Run as administrator" is offered on Windows only.</summary>
public sealed class RunStepFormTests
{
    [AvaloniaFact]
    public void TheRegistryFindsTheFormAndItShowsTheStep()
    {
        Assert.True(StepFormRegistry.Default.Supports(RunStepType.Instance));

        var (form, _) = Show(new RunStep("taskkill.exe", "/f /im synthetic-emulator.exe", @"C:\Temp", Elevated: true, Hidden: true), _ => { });

        Assert.Equal(["taskkill.exe", "/f /im synthetic-emulator.exe", @"C:\Temp"], form.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text));
        string[] expected = RunStepForm.OffersElevation
            ? ["Program", "Arguments", "Start in", RunStepForm.ElevatedLabel, RunStepForm.HiddenLabel]
            : ["Program", "Arguments", "Start in", RunStepForm.HiddenLabel];
        Assert.Equal(expected, form.GetVisualDescendants().OfType<FieldRow>().Select(row => row.Label));
        Assert.All(form.GetVisualDescendants().OfType<CheckBox>(), box => Assert.True(box.IsChecked));
        Assert.Contains(form.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, RunStepForm.BrowseLabel));
    }

    [AvaloniaFact]
    public void EveryFieldEmitsTheEditedStep()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(RunStep.Unset, changes.Add);
        var boxes = form.GetVisualDescendants().OfType<TextBox>().ToList();

        boxes[0].Text = "explorer";
        Assert.Equal(new RunStep("explorer"), Assert.Single(changes));

        boxes[1].Text = "/select,C:\\Temp";
        Assert.Equal(new RunStep("explorer", "/select,C:\\Temp"), changes[^1]);

        boxes[2].Text = "C:\\Temp";
        Assert.Equal(new RunStep("explorer", "/select,C:\\Temp", "C:\\Temp"), changes[^1]);

        form.GetVisualDescendants().OfType<CheckBox>().Last().IsChecked = true;
        Assert.Equal(new RunStep("explorer", "/select,C:\\Temp", "C:\\Temp", Hidden: true), changes[^1]);

        if (RunStepForm.OffersElevation)
        {
            form.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = true;
            Assert.Equal(new RunStep("explorer", "/select,C:\\Temp", "C:\\Temp", Elevated: true, Hidden: true), changes[^1]);
        }
    }

    [AvaloniaFact]
    public void AnEditThatChangesNothingEmitsNothing()
    {
        var changes = new List<IStep>();
        var (form, _) = Show(new RunStep("explorer"), changes.Add);

        form.GetVisualDescendants().OfType<TextBox>().First().Text = "explorer";

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
